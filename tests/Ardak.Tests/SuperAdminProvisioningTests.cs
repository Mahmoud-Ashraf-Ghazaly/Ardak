using Ardak.Infrastructure.Data;
using Ardak.Infrastructure.Identity;
using Ardak.Infrastructure.Services;
using Ardak.Model.Constants;
using Ardak.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ardak.Tests;

public class SuperAdminProvisioningTests
{
    [Fact]
    public async Task SeedAsync_CreatesRolesAndPrimarySuperAdmin_WhenNoneExist()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        var options = Options.Create(new SuperAdminOptions
        {
            Email = "superadmin@ardak.internal",
            Password = "SecurePassword123!",
            FullName = "Primary SuperAdmin"
        });

        var seeder = new SuperAdminSeeder(
            userManager,
            roleManager,
            options,
            NullLogger<SuperAdminSeeder>.Instance);

        await seeder.SeedAsync();

        // Assert roles exist
        Assert.True(await roleManager.RoleExistsAsync(AppRoles.SuperAdmin));
        Assert.True(await roleManager.RoleExistsAsync(AppRoles.Admin));
        Assert.True(await roleManager.RoleExistsAsync(AppRoles.Customer));

        // Assert user exists and is SuperAdmin
        var user = await userManager.FindByEmailAsync("superadmin@ardak.internal");
        Assert.NotNull(user);
        Assert.True(user.IsPrimarySuperAdmin);
        Assert.True(user.IsActive);
        Assert.True(await userManager.IsInRoleAsync(user, AppRoles.SuperAdmin));
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_AndNeverResetsPassword_WhenUserAlreadyExists()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        var options = Options.Create(new SuperAdminOptions
        {
            Email = "existing.superadmin@ardak.internal",
            Password = "InitialPassword123!",
            FullName = "Existing SuperAdmin"
        });

        var seeder = new SuperAdminSeeder(
            userManager,
            roleManager,
            options,
            NullLogger<SuperAdminSeeder>.Instance);

        // First seed
        await seeder.SeedAsync();

        var user = await userManager.FindByEmailAsync("existing.superadmin@ardak.internal");
        Assert.NotNull(user);

        // User changes their password
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var changeResult = await userManager.ResetPasswordAsync(user, token, "UserChangedTheirPassword456!");
        Assert.True(changeResult.Succeeded);

        // Run seeder again with the initial password in options (simulating app restart)
        await seeder.SeedAsync();

        // Verify password was NOT overwritten by seeder
        var checkInitialPass = await userManager.CheckPasswordAsync(user, "InitialPassword123!");
        var checkChangedPass = await userManager.CheckPasswordAsync(user, "UserChangedTheirPassword456!");

        Assert.False(checkInitialPass, "Seeder must never reset password back to initial configured value!");
        Assert.True(checkChangedPass, "User's updated password must be preserved across seeding runs.");
    }

    [Fact]
    public async Task ToggleAdminStatusAsync_Fails_WhenAttemptingToDeactivatePrimarySuperAdmin()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var db = services.GetRequiredService<ArdakDbContext>();

        await roleManager.CreateAsync(new IdentityRole(AppRoles.SuperAdmin));

        // Create caller (a SuperAdmin)
        var caller = new ApplicationUser { UserName = "caller@ardak.internal", Email = "caller@ardak.internal", FullName = "Caller SuperAdmin" };
        await userManager.CreateAsync(caller, "Password123!");
        await userManager.AddToRoleAsync(caller, AppRoles.SuperAdmin);

        // Create primary SuperAdmin
        var primarySuperAdmin = new ApplicationUser
        {
            UserName = "primary@ardak.internal",
            Email = "primary@ardak.internal",
            FullName = "Primary SuperAdmin",
            IsPrimarySuperAdmin = true,
            IsActive = true
        };
        await userManager.CreateAsync(primarySuperAdmin, "Password123!");
        await userManager.AddToRoleAsync(primarySuperAdmin, AppRoles.SuperAdmin);

        var identityService = new IdentityService(
            userManager,
            null!,
            db,
            NullLogger<IdentityService>.Instance);

        var result = await identityService.ToggleAdminStatusAsync(primarySuperAdmin.Id, caller.Id);

        Assert.False(result.Succeeded);
        Assert.Contains("primary SuperAdmin", result.Errors.First(), StringComparison.OrdinalIgnoreCase);

        // Verify still active
        var reloaded = await userManager.FindByIdAsync(primarySuperAdmin.Id);
        Assert.True(reloaded!.IsActive);
    }

    [Fact]
    public async Task ToggleAdminStatusAsync_Fails_WhenCallerTriesToDeactivateThemselves()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var db = services.GetRequiredService<ArdakDbContext>();

        await roleManager.CreateAsync(new IdentityRole(AppRoles.SuperAdmin));

        var caller = new ApplicationUser { UserName = "caller@ardak.internal", Email = "caller@ardak.internal", FullName = "Caller SuperAdmin" };
        await userManager.CreateAsync(caller, "Password123!");
        await userManager.AddToRoleAsync(caller, AppRoles.SuperAdmin);

        var identityService = new IdentityService(
            userManager,
            null!,
            db,
            NullLogger<IdentityService>.Instance);

        var result = await identityService.ToggleAdminStatusAsync(caller.Id, caller.Id);

        Assert.False(result.Succeeded);
        Assert.Contains("own account", result.Errors.First(), StringComparison.OrdinalIgnoreCase);
    }
}
