using Ardak.Application.DTOs;
using Ardak.Application.Interfaces;
using Ardak.Infrastructure.Data;
using Ardak.Infrastructure.Identity;
using Ardak.Model.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ardak.Infrastructure.Services;

public class CustomerPortalService : ICustomerPortalService
{
    private readonly ArdakDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<ApplicationUser>? _userManager;
    private readonly ILogger<CustomerPortalService> _logger;

    public CustomerPortalService(
        ArdakDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<CustomerPortalService> logger,
        UserManager<ApplicationUser>? userManager = null)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
        _userManager = userManager;
    }

    // ── Customer Profile ─────────────────────────────────────────────────────

    public async Task<CustomerProfileDto?> GetApprovedCustomerProfileAsync(int customerId, string currentUserId)
    {
        var verifiedCustomerId = await ResolveCustomerIdAsync(currentUserId);
        if (!verifiedCustomerId.HasValue)
        {
            return null;
        }

        // Strict customer isolation: customer may only access their own profile
        if (_currentUserService.IsCustomer || verifiedCustomerId.Value != customerId)
        {
            if (verifiedCustomerId.Value != customerId && !_currentUserService.IsSuperAdmin && !_currentUserService.IsAdmin)
            {
                _logger.LogWarning("Access violation: User '{UserId}' attempted to access profile for Customer ID {TargetCustomerId}.",
                    currentUserId, customerId);
                return null;
            }
        }

        var customer = await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer == null)
        {
            return null;
        }

        // Return CustomerProfileDto containing strictly approved display fields
        // InternalNotes are never exposed
        return new CustomerProfileDto
        {
            Id = customer.Id,
            FullName = customer.FullName,
            Email = customer.Email ?? string.Empty,
            PhoneNumber = customer.PhoneNumber,
            Address = customer.Address,
            NationalIdOrTaxNumber = customer.NationalIdOrTaxNumber,
            IsActive = customer.IsActive,
            MemberSinceUtc = customer.CreatedAtUtc,
            HasPortalAccount = !string.IsNullOrEmpty(customer.IdentityUserId)
        };
    }

    // ── Customer Dashboard ───────────────────────────────────────────────────

    public async Task<CustomerPortalDashboardDto?> GetCustomerDashboardAsync(string currentUserId)
    {
        var customerId = await ResolveCustomerIdAsync(currentUserId);
        if (!customerId.HasValue)
        {
            return null;
        }

        var profile = await GetApprovedCustomerProfileAsync(customerId.Value, currentUserId);
        if (profile == null)
        {
            return null;
        }

        var properties = await GetCustomerPropertiesAsync(currentUserId);
        var documents = await GetCustomerDocumentsAsync(currentUserId);

        // Fetch customer-visible procedures across all owned/associated properties
        var approvedProcedures = await _dbContext.Procedures
            .AsNoTracking()
            .Include(p => p.Property)
                .ThenInclude(pr => pr!.CustomerAssociations)
            .Where(p =>
                p.IsCustomerVisible &&
                p.Property != null &&
                (p.Property.CustomerId == customerId.Value ||
                 p.Property.CustomerAssociations.Any(ca => ca.CustomerId == customerId.Value)))
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new CustomerProcedureDto
            {
                Id = p.Id,
                PropertyId = p.PropertyId,
                PropertyReferenceNumber = p.Property != null ? p.Property.ReferenceNumber : string.Empty,
                PropertyTitle = p.Property != null ? p.Property.Title : string.Empty,
                Type = p.Type,
                TypeLabel = p.TypeLabel ?? p.Type.ToString(),
                ApplicationNumber = p.ApplicationNumber,
                ResponsibleAuthority = p.ResponsibleAuthority,
                Status = p.Status,
                SubmissionDate = p.SubmissionDate,
                CustomerUpdateNotes = p.CustomerUpdateNotes,
                CreatedAtUtc = p.CreatedAtUtc
            })
            .ToListAsync();

        return new CustomerPortalDashboardDto
        {
            Profile = profile,
            Properties = properties,
            ApprovedProcedures = approvedProcedures,
            ApprovedDocuments = documents
        };
    }

    // ── Customer Properties ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<CustomerPropertySummaryDto>> GetCustomerPropertiesAsync(string currentUserId)
    {
        var customerId = await ResolveCustomerIdAsync(currentUserId);
        if (!customerId.HasValue)
        {
            return Array.Empty<CustomerPropertySummaryDto>();
        }

        var custId = customerId.Value;

        var properties = await _dbContext.Properties
            .AsNoTracking()
            .Include(p => p.CustomerAssociations)
            .Include(p => p.Procedures)
            .Include(p => p.Documents)
            .Where(p => p.CustomerId == custId || p.CustomerAssociations.Any(ca => ca.CustomerId == custId))
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync();

        var result = new List<CustomerPropertySummaryDto>();

        foreach (var p in properties)
        {
            var isPrimary = p.CustomerId == custId;
            var association = p.CustomerAssociations.FirstOrDefault(ca => ca.CustomerId == custId);

            result.Add(new CustomerPropertySummaryDto
            {
                Id = p.Id,
                ReferenceNumber = p.ReferenceNumber,
                Title = p.Title,
                Type = p.Type,
                City = p.City,
                DistrictOrArea = p.DistrictOrArea,
                AreaMeasurement = p.AreaMeasurement,
                AreaUnit = p.AreaUnit,
                Status = p.Status,
                OwnershipRole = isPrimary ? "Primary Owner" : (association?.OwnershipRole ?? "Associated"),
                OwnershipPercentage = isPrimary ? 100m : association?.OwnershipPercentage,
                ApprovedProceduresCount = p.Procedures.Count(pr => pr.IsCustomerVisible),
                ApprovedDocumentsCount = p.Documents.Count(d => d.IsCustomerVisible && !d.IsArchived)
            });
        }

        return result;
    }

    // ── Customer Property Detail ─────────────────────────────────────────────

    public async Task<CustomerPropertyDetailDto?> GetCustomerPropertyDetailAsync(int propertyId, string currentUserId)
    {
        var customerId = await ResolveCustomerIdAsync(currentUserId);
        if (!customerId.HasValue)
        {
            return null;
        }

        var custId = customerId.Value;

        var p = await _dbContext.Properties
            .AsNoTracking()
            .Include(prop => prop.CustomerAssociations)
            .Include(prop => prop.Procedures)
            .Include(prop => prop.Documents)
            .FirstOrDefaultAsync(prop => prop.Id == propertyId);

        if (p == null)
        {
            return null;
        }

        // Strict customer relationship verification
        var isPrimary = p.CustomerId == custId;
        var association = p.CustomerAssociations.FirstOrDefault(ca => ca.CustomerId == custId);

        if (!isPrimary && association == null && !_currentUserService.IsSuperAdmin && !_currentUserService.IsAdmin)
        {
            _logger.LogWarning("Access violation: Customer ID {CustomerId} attempted to view unauthorized property ID {PropertyId}.",
                custId, propertyId);
            return null;
        }

        // Filter procedures: ONLY approved customer visible
        var visibleProcedures = p.Procedures
            .Where(pr => pr.IsCustomerVisible)
            .OrderByDescending(pr => pr.CreatedAtUtc)
            .Select(pr => new CustomerProcedureDto
            {
                Id = pr.Id,
                PropertyId = p.Id,
                PropertyReferenceNumber = p.ReferenceNumber,
                PropertyTitle = p.Title,
                Type = pr.Type,
                TypeLabel = pr.TypeLabel ?? pr.Type.ToString(),
                ApplicationNumber = pr.ApplicationNumber,
                ResponsibleAuthority = pr.ResponsibleAuthority,
                Status = pr.Status,
                SubmissionDate = pr.SubmissionDate,
                CustomerUpdateNotes = pr.CustomerUpdateNotes,
                CreatedAtUtc = pr.CreatedAtUtc
            })
            .ToList();

        // Filter documents: ONLY approved customer visible and not archived
        var visibleDocuments = p.Documents
            .Where(d => d.IsCustomerVisible && !d.IsArchived)
            .OrderByDescending(d => d.UploadedAtUtc)
            .Select(d => new CustomerDocumentDto
            {
                Id = d.Id,
                Title = d.Title,
                DocumentType = d.TypeLabel ?? d.Type.ToString(),
                ReferenceNumber = d.ReferenceNumber,
                OriginalFileName = d.OriginalFileName,
                ContentType = d.ContentType,
                FileSizeBytes = d.FileSizeBytes,
                UploadDateUtc = d.UploadedAtUtc
            })
            .ToList();

        return new CustomerPropertyDetailDto
        {
            Id = p.Id,
            ReferenceNumber = p.ReferenceNumber,
            Title = p.Title,
            Description = p.Description,
            Type = p.Type,
            Governorate = p.Governorate,
            City = p.City,
            DistrictOrArea = p.DistrictOrArea,
            AddressDetails = p.AddressDetails,
            AreaMeasurement = p.AreaMeasurement,
            AreaUnit = p.AreaUnit,
            OwnershipType = p.OwnershipType,
            DeedOrTitleNumber = p.DeedOrTitleNumber,
            Status = p.Status,
            OwnershipRole = isPrimary ? "Primary Owner" : (association?.OwnershipRole ?? "Associated"),
            OwnershipPercentage = isPrimary ? 100m : association?.OwnershipPercentage,
            ApprovedProcedures = visibleProcedures,
            ApprovedDocuments = visibleDocuments
        };
    }

    // ── Customer Procedure Detail ────────────────────────────────────────────

    public async Task<CustomerProcedureDto?> GetCustomerProcedureDetailAsync(int procedureId, string currentUserId)
    {
        var customerId = await ResolveCustomerIdAsync(currentUserId);
        if (!customerId.HasValue)
        {
            return null;
        }

        var custId = customerId.Value;

        var pr = await _dbContext.Procedures
            .AsNoTracking()
            .Include(p => p.Property)
                .ThenInclude(prop => prop!.CustomerAssociations)
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(p => p.Id == procedureId);

        if (pr == null)
        {
            return null;
        }

        // Procedure must be explicitly customer-visible
        if (!pr.IsCustomerVisible)
        {
            _logger.LogInformation("Procedure ID {ProcedureId} access denied to customer: not approved for customer visibility.", procedureId);
            return null;
        }

        // Verify customer owns or is associated with the procedure's property
        var prop = pr.Property;
        if (prop == null)
        {
            return null;
        }

        var isAssociated = prop.CustomerId == custId || prop.CustomerAssociations.Any(ca => ca.CustomerId == custId);
        if (!isAssociated && !_currentUserService.IsSuperAdmin && !_currentUserService.IsAdmin)
        {
            _logger.LogWarning("Access violation: Customer ID {CustomerId} attempted to view procedure ID {ProcedureId} on unassociated property {PropertyId}.",
                custId, procedureId, prop.Id);
            return null;
        }

        // Filter linked documents: only approved and not archived
        var visibleDocs = pr.Documents
            .Where(d => d.IsCustomerVisible && !d.IsArchived)
            .OrderByDescending(d => d.UploadedAtUtc)
            .Select(d => new CustomerDocumentDto
            {
                Id = d.Id,
                Title = d.Title,
                DocumentType = d.TypeLabel ?? d.Type.ToString(),
                ReferenceNumber = d.ReferenceNumber,
                OriginalFileName = d.OriginalFileName,
                ContentType = d.ContentType,
                FileSizeBytes = d.FileSizeBytes,
                UploadDateUtc = d.UploadedAtUtc
            })
            .ToList();

        return new CustomerProcedureDto
        {
            Id = pr.Id,
            PropertyId = pr.PropertyId,
            PropertyReferenceNumber = prop.ReferenceNumber,
            PropertyTitle = prop.Title,
            Type = pr.Type,
            TypeLabel = pr.TypeLabel ?? pr.Type.ToString(),
            ApplicationNumber = pr.ApplicationNumber,
            ResponsibleAuthority = pr.ResponsibleAuthority,
            Status = pr.Status,
            SubmissionDate = pr.SubmissionDate,
            CustomerUpdateNotes = pr.CustomerUpdateNotes,
            CreatedAtUtc = pr.CreatedAtUtc,
            ApprovedDocuments = visibleDocs
        };
    }

    // ── Customer Documents ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<CustomerDocumentDto>> GetCustomerDocumentsAsync(string currentUserId)
    {
        var customerId = await ResolveCustomerIdAsync(currentUserId);
        if (!customerId.HasValue)
        {
            return Array.Empty<CustomerDocumentDto>();
        }

        var custId = customerId.Value;

        return await _dbContext.Documents
            .AsNoTracking()
            .Include(d => d.Property)
                .ThenInclude(p => p!.CustomerAssociations)
            .Include(d => d.Procedure)
                .ThenInclude(pr => pr!.Property)
                    .ThenInclude(prp => prp!.CustomerAssociations)
            .Where(d =>
                d.IsCustomerVisible &&
                !d.IsArchived &&
                (
                    (d.CustomerId.HasValue && d.CustomerId.Value == custId) ||
                    (d.Property != null && (d.Property.CustomerId == custId || d.Property.CustomerAssociations.Any(ca => ca.CustomerId == custId))) ||
                    (d.Procedure != null && d.Procedure.Property != null && (d.Procedure.Property.CustomerId == custId || d.Procedure.Property.CustomerAssociations.Any(ca => ca.CustomerId == custId)))
                ))
            .OrderByDescending(d => d.UploadedAtUtc)
            .Select(d => new CustomerDocumentDto
            {
                Id = d.Id,
                Title = d.Title,
                DocumentType = d.TypeLabel ?? d.Type.ToString(),
                ReferenceNumber = d.ReferenceNumber,
                OriginalFileName = d.OriginalFileName,
                ContentType = d.ContentType,
                FileSizeBytes = d.FileSizeBytes,
                UploadDateUtc = d.UploadedAtUtc
            })
            .ToListAsync();
    }

    // ── Private Helper ───────────────────────────────────────────────────────

    private async Task<int?> ResolveCustomerIdAsync(string currentUserId)
    {
        if (_currentUserService.CustomerId.HasValue)
        {
            return _currentUserService.CustomerId.Value;
        }

        if (_userManager != null && !string.IsNullOrWhiteSpace(currentUserId))
        {
            var user = await _userManager.FindByIdAsync(currentUserId);
            if (user?.CustomerId != null)
            {
                return user.CustomerId.Value;
            }
        }

        return null;
    }
}
