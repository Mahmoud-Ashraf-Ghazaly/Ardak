using Ardak.Application.Interfaces;
using Ardak.Model.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ardak.Infrastructure.Identity;

public class SuperAdminSeeder : ISuperAdminSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IOptions<SuperAdminOptions> _options;
    private readonly ILogger<SuperAdminSeeder> _logger;

    public SuperAdminSeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<SuperAdminOptions> options,
        ILogger<SuperAdminSeeder> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _options = options;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        // 1. Ensure Roles exist (Idempotent)
        string[] roles = [AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.Customer];
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                var result = await _roleManager.CreateAsync(new IdentityRole(role));
                if (result.Succeeded)
                {
                    _logger.LogInformation("Provisioned role {Role}", role);
                }
                else
                {
                    _logger.LogError("Failed to create role {Role}: {Errors}", role, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        // 2. Read SuperAdmin credentials from options or environment variables
        var email = Environment.GetEnvironmentVariable("SUPERADMIN_EMAIL");
        if (string.IsNullOrWhiteSpace(email))
        {
            email = _options.Value.Email;
        }

        var password = Environment.GetEnvironmentVariable("SUPERADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            password = _options.Value.Password;
        }

        var fullName = Environment.GetEnvironmentVariable("SUPERADMIN_FULLNAME");
        if (string.IsNullOrWhiteSpace(fullName))
        {
            fullName = _options.Value.FullName;
            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = "Primary SuperAdmin";
            }
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("SuperAdmin provisioning skipped: No credentials configured in SuperAdmin configuration or environment variables.");
            return;
        }

        // 3. Check if SuperAdmin already exists (by email or primary flag)
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            // Idempotent: NEVER reset or overwrite password automatically during startup
            _logger.LogInformation("SuperAdmin account '{Email}' already exists. Preserving existing credentials.", email);

            bool needsUpdate = false;
            if (!existingUser.IsPrimarySuperAdmin)
            {
                existingUser.IsPrimarySuperAdmin = true;
                needsUpdate = true;
            }

            if (!existingUser.IsActive)
            {
                existingUser.IsActive = true;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                await _userManager.UpdateAsync(existingUser);
            }

            if (!await _userManager.IsInRoleAsync(existingUser, AppRoles.SuperAdmin))
            {
                await _userManager.AddToRoleAsync(existingUser, AppRoles.SuperAdmin);
                _logger.LogInformation("Assigned SuperAdmin role to existing user '{Email}'.", email);
            }

            return;
        }

        // 4. Create primary SuperAdmin user
        var primarySuperAdmin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = true,
            IsActive = true,
            IsPrimarySuperAdmin = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(primarySuperAdmin, password);
        if (createResult.Succeeded)
        {
            await _userManager.AddToRoleAsync(primarySuperAdmin, AppRoles.SuperAdmin);
            _logger.LogInformation("Successfully provisioned primary SuperAdmin '{Email}'.", email);
        }
        else
        {
            _logger.LogError("Failed to provision primary SuperAdmin '{Email}': {Errors}",
                email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }
    }
}
