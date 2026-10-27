using System.Reflection;
using Ardak.Application.DTOs;
using Ardak.Infrastructure.Data;
using Ardak.Infrastructure.Identity;
using Ardak.Infrastructure.Services;
using Ardak.Model.Constants;
using Ardak.Model.Entities;
using Ardak.Tests.Helpers;
using landy.Controllers;
using landy.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ardak.Tests;

public class RoleBasedAccessTests
{
    [Fact]
    public async Task CreateAdminAsync_Succeeds_WhenCallerIsSuperAdmin()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var db = services.GetRequiredService<ArdakDbContext>();

        await roleManager.CreateAsync(new IdentityRole(AppRoles.SuperAdmin));
        await roleManager.CreateAsync(new IdentityRole(AppRoles.Admin));

        var superAdmin = new ApplicationUser { UserName = "super@ardak.internal", Email = "super@ardak.internal", FullName = "Super Admin" };
        await userManager.CreateAsync(superAdmin, "Password123!");
        await userManager.AddToRoleAsync(superAdmin, AppRoles.SuperAdmin);

        var identityService = new IdentityService(userManager, null!, db, NullLogger<IdentityService>.Instance);

        var dto = new CreateAdminDto
        {
            Email = "newadmin@ardak.internal",
            FullName = "New Admin",
            Password = "AdminPassword123!",
            ConfirmPassword = "AdminPassword123!"
        };

        var result = await identityService.CreateAdminAsync(dto, superAdmin.Id);

        Assert.True(result.Succeeded);
        var created = await userManager.FindByEmailAsync("newadmin@ardak.internal");
        Assert.NotNull(created);
        Assert.True(await userManager.IsInRoleAsync(created, AppRoles.Admin));
        Assert.False(await userManager.IsInRoleAsync(created, AppRoles.SuperAdmin));
    }

    [Fact]
    public async Task CreateAdminAsync_Fails_WhenCallerIsNotSuperAdmin()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var db = services.GetRequiredService<ArdakDbContext>();

        await roleManager.CreateAsync(new IdentityRole(AppRoles.Admin));

        // Caller is only an Admin (cannot create other Admins)
        var regularAdmin = new ApplicationUser { UserName = "admin@ardak.internal", Email = "admin@ardak.internal", FullName = "Regular Admin" };
        await userManager.CreateAsync(regularAdmin, "Password123!");
        await userManager.AddToRoleAsync(regularAdmin, AppRoles.Admin);

        var identityService = new IdentityService(userManager, null!, db, NullLogger<IdentityService>.Instance);

        var dto = new CreateAdminDto
        {
            Email = "illegal@ardak.internal",
            FullName = "Illegal Admin",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        var result = await identityService.CreateAdminAsync(dto, regularAdmin.Id);

        Assert.False(result.Succeeded);
        Assert.Contains("Only a SuperAdmin", result.Errors.First(), StringComparison.OrdinalIgnoreCase);

        var notCreated = await userManager.FindByEmailAsync("illegal@ardak.internal");
        Assert.Null(notCreated);
    }

    [Fact]
    public async Task CreateCustomerAccountAsync_AssignsCustomerRole_NeverAdministrativeRole()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var db = services.GetRequiredService<ArdakDbContext>();

        await roleManager.CreateAsync(new IdentityRole(AppRoles.Admin));
        await roleManager.CreateAsync(new IdentityRole(AppRoles.Customer));

        var adminCaller = new ApplicationUser { UserName = "staff@ardak.internal", Email = "staff@ardak.internal", FullName = "Staff Admin" };
        await userManager.CreateAsync(adminCaller, "Password123!");
        await userManager.AddToRoleAsync(adminCaller, AppRoles.Admin);

        var customer = new Customer
        {
            FullName = "Client Corp",
            Email = "client@corp.internal",
            PhoneNumber = "555-1234",
            InternalNotes = "Private financial notes"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var identityService = new IdentityService(userManager, null!, db, NullLogger<IdentityService>.Instance);

        var dto = new CreateCustomerAccountDto
        {
            CustomerId = customer.Id,
            Email = "portal@corp.internal",
            Password = "ClientPassword123!",
            ConfirmPassword = "ClientPassword123!"
        };

        var result = await identityService.CreateCustomerAccountAsync(dto, adminCaller.Id);

        Assert.True(result.Succeeded);

        var customerUser = await userManager.FindByEmailAsync("portal@corp.internal");
        Assert.NotNull(customerUser);
        Assert.Equal(customer.Id, customerUser.CustomerId);
        Assert.True(await userManager.IsInRoleAsync(customerUser, AppRoles.Customer));
        Assert.False(await userManager.IsInRoleAsync(customerUser, AppRoles.Admin));
        Assert.False(await userManager.IsInRoleAsync(customerUser, AppRoles.SuperAdmin));

        // Reload customer entity to verify link
        var updatedCustomer = await db.Customers.FindAsync(customer.Id);
        Assert.Equal(customerUser.Id, updatedCustomer!.IdentityUserId);
    }

    [Fact]
    public void Controllers_EnforceStrictServerSideAuthorization()
    {
        // AdminManagementController must require SuperAdmin policy
        var adminAttr = typeof(AdminManagementController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(adminAttr);
        Assert.Equal(AppPolicies.RequireSuperAdmin, adminAttr.Policy);

        // InternalDashboardController must require AdminOrSuperAdmin policy
        var dashAttr = typeof(InternalDashboardController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(dashAttr);
        Assert.Equal(AppPolicies.RequireAdminOrSuperAdmin, dashAttr.Policy);

        // CustomerAccountController must require AdminOrSuperAdmin policy
        var custAccAttr = typeof(CustomerAccountController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(custAccAttr);
        Assert.Equal(AppPolicies.RequireAdminOrSuperAdmin, custAccAttr.Policy);

        // CustomerPortalController must require Customer policy
        var portalAttr = typeof(CustomerPortalController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(portalAttr);
        Assert.Equal(AppPolicies.RequireCustomer, portalAttr.Policy);

        // CustomersController must require AdminOrSuperAdmin policy
        var custAttr = typeof(CustomersController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(custAttr);
        Assert.Equal(AppPolicies.RequireAdminOrSuperAdmin, custAttr.Policy);

        // PropertiesController must require AdminOrSuperAdmin policy
        var propAttr = typeof(PropertiesController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(propAttr);
        Assert.Equal(AppPolicies.RequireAdminOrSuperAdmin, propAttr.Policy);

        // ProceduresController must require AdminOrSuperAdmin policy
        var procAttr = typeof(ProceduresController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(procAttr);
        Assert.Equal(AppPolicies.RequireAdminOrSuperAdmin, procAttr.Policy);

        // DocumentsController must require AdminOrSuperAdmin policy
        var docAttr = typeof(DocumentsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(docAttr);
        Assert.Equal(AppPolicies.RequireAdminOrSuperAdmin, docAttr.Policy);

        // AccountController must allow anonymous for login, but have anti-forgery on Login POST
        var loginPostMethod = typeof(AccountController).GetMethod("Login", [typeof(LoginViewModel)]);
        Assert.NotNull(loginPostMethod);
        Assert.NotNull(loginPostMethod.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }
}
