using System.Security.Claims;
using Ardak.Application.DTOs;
using Ardak.Application.Interfaces;
using Ardak.Infrastructure.Data;
using Ardak.Infrastructure.Identity;
using Ardak.Model.Constants;
using Ardak.Model.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ardak.Infrastructure.Services;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ArdakDbContext _dbContext;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ArdakDbContext dbContext,
        ILogger<IdentityService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AdminUserDto>> GetAdminsAsync()
    {
        var superAdmins = await _userManager.GetUsersInRoleAsync(AppRoles.SuperAdmin);
        var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);

        var list = new List<AdminUserDto>();

        foreach (var user in superAdmins)
        {
            list.Add(new AdminUserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                IsActive = user.IsActive,
                IsPrimarySuperAdmin = user.IsPrimarySuperAdmin,
                Role = AppRoles.SuperAdmin,
                CreatedAtUtc = user.CreatedAtUtc
            });
        }

        foreach (var user in admins)
        {
            if (list.All(u => u.Id != user.Id))
            {
                list.Add(new AdminUserDto
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    FullName = user.FullName,
                    IsActive = user.IsActive,
                    IsPrimarySuperAdmin = user.IsPrimarySuperAdmin,
                    Role = AppRoles.Admin,
                    CreatedAtUtc = user.CreatedAtUtc
                });
            }
        }

        return list.OrderByDescending(u => u.IsPrimarySuperAdmin)
                   .ThenBy(u => u.Role)
                   .ThenBy(u => u.FullName)
                   .ToList();
    }

    public async Task<IdentityOperationResult> CreateAdminAsync(CreateAdminDto dto, string currentUserId)
    {
        var caller = await _userManager.FindByIdAsync(currentUserId);
        if (caller == null || !await _userManager.IsInRoleAsync(caller, AppRoles.SuperAdmin))
        {
            return IdentityOperationResult.Failure("Access denied: Only a SuperAdmin can create Admin accounts.");
        }

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return IdentityOperationResult.Failure($"A user with email '{dto.Email}' already exists.");
        }

        var newAdmin = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = dto.FullName,
            EmailConfirmed = true,
            IsActive = true,
            IsPrimarySuperAdmin = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(newAdmin, dto.Password);
        if (!result.Succeeded)
        {
            return IdentityOperationResult.Failure(result.Errors.Select(e => e.Description));
        }

        // Strict role restriction: Admins are ONLY assigned Admin role, never SuperAdmin
        var roleResult = await _userManager.AddToRoleAsync(newAdmin, AppRoles.Admin);
        if (!roleResult.Succeeded)
        {
            return IdentityOperationResult.Failure(roleResult.Errors.Select(e => e.Description));
        }

        _logger.LogInformation("SuperAdmin '{CallerId}' created new Admin account '{NewAdminEmail}'.", currentUserId, dto.Email);
        return IdentityOperationResult.Success();
    }

    public async Task<IdentityOperationResult> ToggleAdminStatusAsync(string adminUserId, string currentUserId)
    {
        var caller = await _userManager.FindByIdAsync(currentUserId);
        if (caller == null || !await _userManager.IsInRoleAsync(caller, AppRoles.SuperAdmin))
        {
            return IdentityOperationResult.Failure("Access denied: Only a SuperAdmin can manage Admin account status.");
        }

        if (adminUserId == currentUserId)
        {
            return IdentityOperationResult.Failure("You cannot change the active status of your own account.");
        }

        var targetUser = await _userManager.FindByIdAsync(adminUserId);
        if (targetUser == null)
        {
            return IdentityOperationResult.Failure("User account not found.");
        }

        if (targetUser.IsPrimarySuperAdmin)
        {
            return IdentityOperationResult.Failure("The primary SuperAdmin account cannot be deactivated.");
        }

        targetUser.IsActive = !targetUser.IsActive;
        var updateResult = await _userManager.UpdateAsync(targetUser);
        if (!updateResult.Succeeded)
        {
            return IdentityOperationResult.Failure(updateResult.Errors.Select(e => e.Description));
        }

        // Invalidate active session immediately if deactivated
        if (!targetUser.IsActive)
        {
            await _userManager.UpdateSecurityStampAsync(targetUser);
        }

        _logger.LogInformation("SuperAdmin '{CallerId}' toggled active status to {Status} for user '{TargetEmail}'.",
            currentUserId, targetUser.IsActive, targetUser.Email);

        return IdentityOperationResult.Success();
    }

    public async Task<IdentityOperationResult> CreateCustomerAccountAsync(CreateCustomerAccountDto dto, string currentUserId)
    {
        var caller = await _userManager.FindByIdAsync(currentUserId);
        if (caller == null || (!await _userManager.IsInRoleAsync(caller, AppRoles.SuperAdmin) && !await _userManager.IsInRoleAsync(caller, AppRoles.Admin)))
        {
            return IdentityOperationResult.Failure("Access denied: Only staff members can provision customer accounts.");
        }

        var customer = await _dbContext.Customers.FindAsync(dto.CustomerId);
        if (customer == null)
        {
            return IdentityOperationResult.Failure("Customer record not found.");
        }

        if (!string.IsNullOrEmpty(customer.IdentityUserId))
        {
            return IdentityOperationResult.Failure("This customer already has a linked portal login account.");
        }

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return IdentityOperationResult.Failure($"A login account with email '{dto.Email}' already exists.");
        }

        var customerUser = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = customer.FullName,
            EmailConfirmed = true,
            IsActive = true,
            IsPrimarySuperAdmin = false,
            CustomerId = customer.Id,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(customerUser, dto.Password);
        if (!createResult.Succeeded)
        {
            return IdentityOperationResult.Failure(createResult.Errors.Select(e => e.Description));
        }

        // Role isolation: Customers NEVER receive administrative roles
        var roleResult = await _userManager.AddToRoleAsync(customerUser, AppRoles.Customer);
        if (!roleResult.Succeeded)
        {
            return IdentityOperationResult.Failure(roleResult.Errors.Select(e => e.Description));
        }

        // Add customer ID claim for fast claim-based authorization and isolation
        await _userManager.AddClaimAsync(customerUser, new Claim("CustomerId", customer.Id.ToString()));

        // Link customer entity to the new identity user
        customer.IdentityUserId = customerUser.Id;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User '{CallerId}' provisioned customer portal account for Customer ID {CustomerId}.",
            currentUserId, customer.Id);

        return IdentityOperationResult.Success();
    }

    public async Task<IdentityOperationResult> ToggleCustomerAccountStatusAsync(int customerId, string currentUserId)
    {
        var caller = await _userManager.FindByIdAsync(currentUserId);
        if (caller == null || (!await _userManager.IsInRoleAsync(caller, AppRoles.SuperAdmin) && !await _userManager.IsInRoleAsync(caller, AppRoles.Admin)))
        {
            return IdentityOperationResult.Failure("Access denied: Only staff members can manage customer accounts.");
        }

        var customer = await _dbContext.Customers.FindAsync(customerId);
        if (customer == null)
        {
            return IdentityOperationResult.Failure("Customer record not found.");
        }

        customer.Status = customer.Status == CustomerStatus.Active ? CustomerStatus.Inactive : CustomerStatus.Active;
        customer.ModifiedAtUtc = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(customer.IdentityUserId))
        {
            var user = await _userManager.FindByIdAsync(customer.IdentityUserId);
            if (user != null)
            {
                user.IsActive = customer.IsActive;
                await _userManager.UpdateAsync(user);
                if (!user.IsActive)
                {
                    await _userManager.UpdateSecurityStampAsync(user);
                }
            }
        }

        await _dbContext.SaveChangesAsync();
        return IdentityOperationResult.Success();
    }

    public async Task<SignInResultDto> LoginAsync(string email, string password, bool rememberMe)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return SignInResultDto.Failed("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            return SignInResultDto.NotAllowed();
        }

        var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return SignInResultDto.Success();
        }

        if (result.IsLockedOut)
        {
            return SignInResultDto.LockedOut();
        }

        if (result.IsNotAllowed)
        {
            return SignInResultDto.NotAllowed();
        }

        return SignInResultDto.Failed("Invalid email or password.");
    }

    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
    }
}
