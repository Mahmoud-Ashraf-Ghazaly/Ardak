using Ardak.Application.DTOs;
using Ardak.Application.Interfaces;
using Ardak.Infrastructure.Data;
using Ardak.Infrastructure.Identity;
using Ardak.Model.Entities;
using Ardak.Model.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ardak.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly ArdakDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        ArdakDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<CustomerService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CustomerSummaryDto>> SearchCustomersAsync(string? searchTerm = null, CustomerStatus? status = null)
    {
        var query = _dbContext.Customers.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(c => c.FullName.Contains(term) || c.PhoneNumber.Contains(term) || (c.Email != null && c.Email.Contains(term)));
        }

        var customers = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new CustomerSummaryDto
            {
                Id = c.Id,
                FullName = c.FullName,
                PhoneNumber = c.PhoneNumber,
                Email = c.Email,
                Status = c.Status,
                HasPortalAccount = !string.IsNullOrEmpty(c.IdentityUserId),
                PropertyCount = c.Properties.Count,
                CreatedAtUtc = c.CreatedAtUtc
            })
            .ToListAsync();

        return customers;
    }

    public async Task<CustomerDetailDto?> GetCustomerByIdAsync(int id)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .Include(c => c.Properties)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer == null)
        {
            return null;
        }

        return new CustomerDetailDto
        {
            Id = customer.Id,
            FullName = customer.FullName,
            PhoneNumber = customer.PhoneNumber,
            Email = customer.Email,
            Address = customer.Address,
            NationalIdOrTaxNumber = customer.NationalIdOrTaxNumber,
            Status = customer.Status,
            InternalNotes = customer.InternalNotes,
            IdentityUserId = customer.IdentityUserId,
            CreatedAtUtc = customer.CreatedAtUtc,
            ModifiedAtUtc = customer.ModifiedAtUtc,
            PropertyCount = customer.Properties.Count,
            PropertyTitles = customer.Properties.Select(p => p.Title).ToList()
        };
    }

    public async Task<ServiceResult<int>> CreateCustomerAsync(CreateCustomerDto dto, string currentUserId)
    {
        var trimmedPhone = dto.PhoneNumber.Trim();
        var trimmedEmail = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();

        // 1. Prevent accidental duplicate phone numbers among active/inactive records
        var duplicatePhone = await _dbContext.Customers
            .AnyAsync(c => c.PhoneNumber == trimmedPhone && c.Status != CustomerStatus.Archived);

        if (duplicatePhone)
        {
            return ServiceResult<int>.Failure($"A customer record with phone number '{trimmedPhone}' already exists.");
        }

        // 2. Prevent accidental duplicate emails if provided
        if (!string.IsNullOrEmpty(trimmedEmail))
        {
            var duplicateEmail = await _dbContext.Customers
                .AnyAsync(c => c.Email == trimmedEmail && c.Status != CustomerStatus.Archived);

            if (duplicateEmail)
            {
                return ServiceResult<int>.Failure($"A customer record with email '{trimmedEmail}' already exists.");
            }
        }

        var customer = new Customer
        {
            FullName = dto.FullName.Trim(),
            PhoneNumber = trimmedPhone,
            Email = trimmedEmail,
            Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim(),
            NationalIdOrTaxNumber = string.IsNullOrWhiteSpace(dto.NationalIdOrTaxNumber) ? null : dto.NationalIdOrTaxNumber.Trim(),
            InternalNotes = dto.InternalNotes,
            Status = CustomerStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Staff '{UserId}' created customer record ID {CustomerId} ({FullName}).",
            currentUserId, customer.Id, customer.FullName);

        return ServiceResult<int>.Success(customer.Id);
    }

    public async Task<ServiceResult> UpdateCustomerAsync(UpdateCustomerDto dto, string currentUserId)
    {
        var customer = await _dbContext.Customers.FindAsync(dto.Id);
        if (customer == null)
        {
            return ServiceResult.Failure("Customer record not found.");
        }

        var trimmedPhone = dto.PhoneNumber.Trim();
        var trimmedEmail = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();

        // Check duplicate phone (excluding current customer)
        var duplicatePhone = await _dbContext.Customers
            .AnyAsync(c => c.Id != dto.Id && c.PhoneNumber == trimmedPhone && c.Status != CustomerStatus.Archived);

        if (duplicatePhone)
        {
            return ServiceResult.Failure($"Another customer record with phone number '{trimmedPhone}' already exists.");
        }

        // Check duplicate email (excluding current customer)
        if (!string.IsNullOrEmpty(trimmedEmail))
        {
            var duplicateEmail = await _dbContext.Customers
                .AnyAsync(c => c.Id != dto.Id && c.Email == trimmedEmail && c.Status != CustomerStatus.Archived);

            if (duplicateEmail)
            {
                return ServiceResult.Failure($"Another customer record with email '{trimmedEmail}' already exists.");
            }
        }

        var oldStatus = customer.Status;

        customer.FullName = dto.FullName.Trim();
        customer.PhoneNumber = trimmedPhone;
        customer.Email = trimmedEmail;
        customer.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
        customer.NationalIdOrTaxNumber = string.IsNullOrWhiteSpace(dto.NationalIdOrTaxNumber) ? null : dto.NationalIdOrTaxNumber.Trim();
        customer.Status = dto.Status;
        customer.InternalNotes = dto.InternalNotes;
        customer.ModifiedAtUtc = DateTime.UtcNow;

        // If status changed to Inactive or Archived, synchronize linked user account status
        if (customer.Status != CustomerStatus.Active && !string.IsNullOrEmpty(customer.IdentityUserId))
        {
            var user = await _userManager.FindByIdAsync(customer.IdentityUserId);
            if (user != null && user.IsActive)
            {
                user.IsActive = false;
                await _userManager.UpdateAsync(user);
                await _userManager.UpdateSecurityStampAsync(user);
            }
        }
        else if (customer.Status == CustomerStatus.Active && oldStatus != CustomerStatus.Active && !string.IsNullOrEmpty(customer.IdentityUserId))
        {
            var user = await _userManager.FindByIdAsync(customer.IdentityUserId);
            if (user != null && !user.IsActive)
            {
                user.IsActive = true;
                await _userManager.UpdateAsync(user);
            }
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Staff '{UserId}' updated customer record ID {CustomerId}.", currentUserId, customer.Id);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ArchiveCustomerAsync(int id, string currentUserId)
    {
        var customer = await _dbContext.Customers.FindAsync(id);
        if (customer == null)
        {
            return ServiceResult.Failure("Customer record not found.");
        }

        customer.Status = CustomerStatus.Archived;
        customer.ModifiedAtUtc = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(customer.IdentityUserId))
        {
            var user = await _userManager.FindByIdAsync(customer.IdentityUserId);
            if (user != null)
            {
                user.IsActive = false;
                await _userManager.UpdateAsync(user);
                await _userManager.UpdateSecurityStampAsync(user);
            }
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Staff '{UserId}' archived customer record ID {CustomerId}.", currentUserId, id);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> RestoreCustomerAsync(int id, string currentUserId)
    {
        var customer = await _dbContext.Customers.FindAsync(id);
        if (customer == null)
        {
            return ServiceResult.Failure("Customer record not found.");
        }

        customer.Status = CustomerStatus.Active;
        customer.ModifiedAtUtc = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(customer.IdentityUserId))
        {
            var user = await _userManager.FindByIdAsync(customer.IdentityUserId);
            if (user != null)
            {
                user.IsActive = true;
                await _userManager.UpdateAsync(user);
            }
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Staff '{UserId}' restored customer record ID {CustomerId}.", currentUserId, id);
        return ServiceResult.Success();
    }
}
