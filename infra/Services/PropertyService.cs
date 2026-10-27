using Ardak.Application.DTOs;
using Ardak.Application.Interfaces;
using Ardak.Infrastructure.Data;
using Ardak.Model.Entities;
using Ardak.Model.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ardak.Infrastructure.Services;

public class PropertyService : IPropertyService
{
    private readonly ArdakDbContext _dbContext;
    private readonly ILogger<PropertyService> _logger;

    public PropertyService(
        ArdakDbContext dbContext,
        ILogger<PropertyService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PropertySummaryDto>> SearchPropertiesAsync(PropertySearchFilterDto filter)
    {
        var query = _dbContext.Properties
            .AsNoTracking()
            .Include(p => p.Customer)
            .Include(p => p.CustomerAssociations)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.ReferenceNumber))
        {
            var refTerm = filter.ReferenceNumber.Trim();
            query = query.Where(p => p.ReferenceNumber.Contains(refTerm));
        }

        if (!string.IsNullOrWhiteSpace(filter.Location))
        {
            var loc = filter.Location.Trim();
            query = query.Where(p => p.Governorate.Contains(loc) || p.City.Contains(loc) || p.DistrictOrArea.Contains(loc));
        }

        if (filter.Type.HasValue)
        {
            query = query.Where(p => p.Type == filter.Type.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(p => p.Status == filter.Status.Value);
        }

        if (filter.CustomerId.HasValue)
        {
            var custId = filter.CustomerId.Value;
            query = query.Where(p => p.CustomerId == custId || p.CustomerAssociations.Any(ca => ca.CustomerId == custId));
        }

        var properties = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new PropertySummaryDto
            {
                Id = p.Id,
                ReferenceNumber = p.ReferenceNumber,
                Type = p.Type,
                Title = p.Title,
                Governorate = p.Governorate,
                City = p.City,
                DistrictOrArea = p.DistrictOrArea,
                AreaMeasurement = p.AreaMeasurement,
                AreaUnit = p.AreaUnit,
                Status = p.Status,
                PrimaryCustomerId = p.CustomerId,
                PrimaryCustomerName = p.Customer != null ? p.Customer.FullName : "Unknown",
                CoOwnersCount = p.CustomerAssociations.Count(ca => ca.CustomerId != p.CustomerId),
                CreatedAtUtc = p.CreatedAtUtc
            })
            .ToListAsync();

        return properties;
    }

    public async Task<PropertyDetailDto?> GetPropertyByIdAsync(int id)
    {
        var property = await _dbContext.Properties
            .AsNoTracking()
            .Include(p => p.Customer)
            .Include(p => p.CustomerAssociations)
                .ThenInclude(ca => ca.Customer)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (property == null)
        {
            return null;
        }

        return MapToDetailDto(property);
    }

    public async Task<PropertyDetailDto?> GetPropertyByReferenceAsync(string referenceNumber)
    {
        var property = await _dbContext.Properties
            .AsNoTracking()
            .Include(p => p.Customer)
            .Include(p => p.CustomerAssociations)
                .ThenInclude(ca => ca.Customer)
            .FirstOrDefaultAsync(p => p.ReferenceNumber == referenceNumber);

        if (property == null)
        {
            return null;
        }

        return MapToDetailDto(property);
    }

    public async Task<ServiceResult<int>> CreatePropertyAsync(CreatePropertyDto dto, string currentUserId)
    {
        var trimmedRef = dto.ReferenceNumber.Trim();

        // 1. Prevent duplicate reference numbers
        var exists = await _dbContext.Properties.AnyAsync(p => p.ReferenceNumber == trimmedRef);
        if (exists)
        {
            return ServiceResult<int>.Failure($"A property with reference number '{trimmedRef}' already exists.");
        }

        // 2. Validate primary customer exists
        var primaryCustomer = await _dbContext.Customers.FindAsync(dto.PrimaryCustomerId);
        if (primaryCustomer == null)
        {
            return ServiceResult<int>.Failure("The selected primary customer record does not exist.");
        }

        var property = new Property
        {
            ReferenceNumber = trimmedRef,
            Type = dto.Type,
            Title = dto.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            Governorate = dto.Governorate.Trim(),
            City = dto.City.Trim(),
            DistrictOrArea = dto.DistrictOrArea.Trim(),
            AddressDetails = string.IsNullOrWhiteSpace(dto.AddressDetails) ? null : dto.AddressDetails.Trim(),
            AreaMeasurement = dto.AreaMeasurement,
            AreaUnit = string.IsNullOrWhiteSpace(dto.AreaUnit) ? "sqm" : dto.AreaUnit.Trim(),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            OwnershipType = string.IsNullOrWhiteSpace(dto.OwnershipType) ? null : dto.OwnershipType.Trim(),
            DeedOrTitleNumber = string.IsNullOrWhiteSpace(dto.DeedOrTitleNumber) ? null : dto.DeedOrTitleNumber.Trim(),
            Status = PropertyStatus.Active,
            CustomerId = dto.PrimaryCustomerId,
            InternalNotes = dto.InternalNotes,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Associate primary customer
        property.CustomerAssociations.Add(new PropertyCustomer
        {
            CustomerId = dto.PrimaryCustomerId,
            OwnershipRole = "Primary Owner",
            OwnershipPercentage = 100.00m,
            AssociatedAtUtc = DateTime.UtcNow
        });

        _dbContext.Properties.Add(property);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Staff '{UserId}' created property record '{ReferenceNumber}' (ID: {PropertyId}).",
            currentUserId, property.ReferenceNumber, property.Id);

        return ServiceResult<int>.Success(property.Id);
    }

    public async Task<ServiceResult> UpdatePropertyAsync(UpdatePropertyDto dto, string currentUserId)
    {
        var property = await _dbContext.Properties
            .Include(p => p.CustomerAssociations)
            .FirstOrDefaultAsync(p => p.Id == dto.Id);

        if (property == null)
        {
            return ServiceResult.Failure("Property record not found.");
        }

        var trimmedRef = dto.ReferenceNumber.Trim();

        // Check reference uniqueness (excluding current record)
        var duplicateRef = await _dbContext.Properties
            .AnyAsync(p => p.Id != dto.Id && p.ReferenceNumber == trimmedRef);

        if (duplicateRef)
        {
            return ServiceResult.Failure($"Another property with reference number '{trimmedRef}' already exists.");
        }

        // Validate primary customer
        var primaryCustomer = await _dbContext.Customers.FindAsync(dto.PrimaryCustomerId);
        if (primaryCustomer == null)
        {
            return ServiceResult.Failure("The selected primary customer record does not exist.");
        }

        property.ReferenceNumber = trimmedRef;
        property.Type = dto.Type;
        property.Title = dto.Title.Trim();
        property.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        property.Governorate = dto.Governorate.Trim();
        property.City = dto.City.Trim();
        property.DistrictOrArea = dto.DistrictOrArea.Trim();
        property.AddressDetails = string.IsNullOrWhiteSpace(dto.AddressDetails) ? null : dto.AddressDetails.Trim();
        property.AreaMeasurement = dto.AreaMeasurement;
        property.AreaUnit = string.IsNullOrWhiteSpace(dto.AreaUnit) ? "sqm" : dto.AreaUnit.Trim();
        property.Latitude = dto.Latitude;
        property.Longitude = dto.Longitude;
        property.OwnershipType = string.IsNullOrWhiteSpace(dto.OwnershipType) ? null : dto.OwnershipType.Trim();
        property.DeedOrTitleNumber = string.IsNullOrWhiteSpace(dto.DeedOrTitleNumber) ? null : dto.DeedOrTitleNumber.Trim();
        property.Status = dto.Status;
        property.InternalNotes = dto.InternalNotes;
        property.ModifiedAtUtc = DateTime.UtcNow;

        // If primary customer changed, ensure customer association is present
        if (property.CustomerId != dto.PrimaryCustomerId)
        {
            property.CustomerId = dto.PrimaryCustomerId;
            var existingAssoc = property.CustomerAssociations.FirstOrDefault(ca => ca.CustomerId == dto.PrimaryCustomerId);
            if (existingAssoc == null)
            {
                property.CustomerAssociations.Add(new PropertyCustomer
                {
                    CustomerId = dto.PrimaryCustomerId,
                    OwnershipRole = "Primary Owner",
                    AssociatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                existingAssoc.OwnershipRole = "Primary Owner";
            }
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Staff '{UserId}' updated property record ID {PropertyId} ({ReferenceNumber}).",
            currentUserId, property.Id, property.ReferenceNumber);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ArchivePropertyAsync(int id, string currentUserId)
    {
        var property = await _dbContext.Properties.FindAsync(id);
        if (property == null)
        {
            return ServiceResult.Failure("Property record not found.");
        }

        property.Status = PropertyStatus.Archived;
        property.ModifiedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Staff '{UserId}' archived property record ID {PropertyId}.", currentUserId, id);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> RestorePropertyAsync(int id, string currentUserId)
    {
        var property = await _dbContext.Properties.FindAsync(id);
        if (property == null)
        {
            return ServiceResult.Failure("Property record not found.");
        }

        property.Status = PropertyStatus.Active;
        property.ModifiedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Staff '{UserId}' restored property record ID {PropertyId} to active.", currentUserId, id);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> AssociateCustomerAsync(int propertyId, int customerId, string role, decimal? ownershipPercentage, string currentUserId)
    {
        var property = await _dbContext.Properties
            .Include(p => p.CustomerAssociations)
            .FirstOrDefaultAsync(p => p.Id == propertyId);

        if (property == null)
        {
            return ServiceResult.Failure("Property record not found.");
        }

        var customer = await _dbContext.Customers.FindAsync(customerId);
        if (customer == null)
        {
            return ServiceResult.Failure("Customer record not found.");
        }

        var existingAssoc = property.CustomerAssociations.FirstOrDefault(ca => ca.CustomerId == customerId);
        if (existingAssoc != null)
        {
            existingAssoc.OwnershipRole = string.IsNullOrWhiteSpace(role) ? "Co-Owner" : role.Trim();
            existingAssoc.OwnershipPercentage = ownershipPercentage;
        }
        else
        {
            property.CustomerAssociations.Add(new PropertyCustomer
            {
                PropertyId = propertyId,
                CustomerId = customerId,
                OwnershipRole = string.IsNullOrWhiteSpace(role) ? "Co-Owner" : role.Trim(),
                OwnershipPercentage = ownershipPercentage,
                AssociatedAtUtc = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Staff '{UserId}' associated customer ID {CustomerId} with property ID {PropertyId} (Role: {Role}).",
            currentUserId, customerId, propertyId, role);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> RemoveCustomerAssociationAsync(int propertyId, int customerId, string currentUserId)
    {
        var property = await _dbContext.Properties
            .Include(p => p.CustomerAssociations)
            .FirstOrDefaultAsync(p => p.Id == propertyId);

        if (property == null)
        {
            return ServiceResult.Failure("Property record not found.");
        }

        if (property.CustomerId == customerId)
        {
            return ServiceResult.Failure("Cannot remove the primary customer association. Reassign the primary customer first.");
        }

        var assoc = property.CustomerAssociations.FirstOrDefault(ca => ca.CustomerId == customerId);
        if (assoc == null)
        {
            return ServiceResult.Failure("Association not found.");
        }

        property.CustomerAssociations.Remove(assoc);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Staff '{UserId}' removed customer association ID {CustomerId} from property ID {PropertyId}.",
            currentUserId, customerId, propertyId);

        return ServiceResult.Success();
    }

    private static PropertyDetailDto MapToDetailDto(Property property)
    {
        return new PropertyDetailDto
        {
            Id = property.Id,
            ReferenceNumber = property.ReferenceNumber,
            Type = property.Type,
            Title = property.Title,
            Description = property.Description,
            Governorate = property.Governorate,
            City = property.City,
            DistrictOrArea = property.DistrictOrArea,
            AddressDetails = property.AddressDetails,
            AreaMeasurement = property.AreaMeasurement,
            AreaUnit = property.AreaUnit,
            Latitude = property.Latitude,
            Longitude = property.Longitude,
            OwnershipType = property.OwnershipType,
            DeedOrTitleNumber = property.DeedOrTitleNumber,
            Status = property.Status,
            InternalNotes = property.InternalNotes,
            PrimaryCustomerId = property.CustomerId,
            PrimaryCustomerName = property.Customer != null ? property.Customer.FullName : "Unknown",
            PrimaryCustomerPhone = property.Customer != null ? property.Customer.PhoneNumber : string.Empty,
            AssociatedCustomers = property.CustomerAssociations
                .Select(ca => new AssociatedCustomerDto
                {
                    CustomerId = ca.CustomerId,
                    CustomerName = ca.Customer != null ? ca.Customer.FullName : "Unknown",
                    CustomerPhone = ca.Customer != null ? ca.Customer.PhoneNumber : string.Empty,
                    CustomerEmail = ca.Customer?.Email,
                    OwnershipRole = ca.OwnershipRole,
                    OwnershipPercentage = ca.OwnershipPercentage,
                    AssociatedAtUtc = ca.AssociatedAtUtc
                })
                .OrderBy(ca => ca.OwnershipRole == "Primary Owner" ? 0 : 1)
                .ThenBy(ca => ca.CustomerName)
                .ToList(),
            CreatedAtUtc = property.CreatedAtUtc,
            ModifiedAtUtc = property.ModifiedAtUtc
        };
    }
}
