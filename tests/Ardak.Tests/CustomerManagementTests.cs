using System.Reflection;
using Ardak.Application.DTOs;
using Ardak.Infrastructure.Data;
using Ardak.Infrastructure.Identity;
using Ardak.Infrastructure.Services;
using Ardak.Model.Constants;
using Ardak.Model.Entities;
using Ardak.Model.Enums;
using Ardak.Tests.Helpers;
using landy.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ardak.Tests;

public class CustomerManagementTests
{
    [Fact]
    public async Task CreateCustomerAsync_Succeeds_AndSetsTimestampsAndStatus()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var customerService = new CustomerService(db, userManager, NullLogger<CustomerService>.Instance);

        var dto = new CreateCustomerDto
        {
            FullName = "Al-Rashid Enterprises",
            PhoneNumber = "+966501112233",
            Email = "contact@alrashid.internal",
            Address = "Al-Olaya District, Riyadh",
            NationalIdOrTaxNumber = "300123456700003",
            InternalNotes = "High priority corporate client."
        };

        var result = await customerService.CreateCustomerAsync(dto, "staff-user-id");

        Assert.True(result.Succeeded);
        Assert.True(result.Data > 0);

        var saved = await db.Customers.FindAsync(result.Data);
        Assert.NotNull(saved);
        Assert.Equal("Al-Rashid Enterprises", saved.FullName);
        Assert.Equal("+966501112233", saved.PhoneNumber);
        Assert.Equal("contact@alrashid.internal", saved.Email);
        Assert.Equal(CustomerStatus.Active, saved.Status);
        Assert.True(saved.IsActive);
        Assert.Equal("High priority corporate client.", saved.InternalNotes);
        Assert.True(saved.CreatedAtUtc <= DateTime.UtcNow);
        Assert.Null(saved.ModifiedAtUtc);
    }

    [Fact]
    public async Task CreateCustomerAsync_PreventsDuplicatePhoneNumber()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var customerService = new CustomerService(db, userManager, NullLogger<CustomerService>.Instance);

        // Add existing customer
        var existing = new Customer
        {
            FullName = "Original Client",
            PhoneNumber = "0555999888",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(existing);
        await db.SaveChangesAsync();

        // Attempt to create another customer with the same phone number
        var duplicateDto = new CreateCustomerDto
        {
            FullName = "Duplicate Client",
            PhoneNumber = "0555999888",
            Email = "unique@client.internal"
        };

        var result = await customerService.CreateCustomerAsync(duplicateDto, "staff-user-id");

        Assert.False(result.Succeeded);
        Assert.Contains("phone number", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateCustomerAsync_PreventsDuplicateEmail_WhenEmailProvided()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var customerService = new CustomerService(db, userManager, NullLogger<CustomerService>.Instance);

        var existing = new Customer
        {
            FullName = "First Client",
            PhoneNumber = "0501111111",
            Email = "shared@domain.internal",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(existing);
        await db.SaveChangesAsync();

        var duplicateEmailDto = new CreateCustomerDto
        {
            FullName = "Second Client",
            PhoneNumber = "0502222222",
            Email = "shared@domain.internal"
        };

        var result = await customerService.CreateCustomerAsync(duplicateEmailDto, "staff-user-id");

        Assert.False(result.Succeeded);
        Assert.Contains("email", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchCustomersAsync_MatchesByName_AndByPhoneNumber()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        db.Customers.AddRange(
            new Customer { FullName = "Fahad Al-Harbi", PhoneNumber = "0501234567", Status = CustomerStatus.Active },
            new Customer { FullName = "Khalid Mansour", PhoneNumber = "0559876543", Status = CustomerStatus.Active },
            new Customer { FullName = "Mansour Real Estate", PhoneNumber = "0560001122", Status = CustomerStatus.Inactive }
        );
        await db.SaveChangesAsync();

        var customerService = new CustomerService(db, userManager, NullLogger<CustomerService>.Instance);

        // Search by partial name
        var nameResults = await customerService.SearchCustomersAsync("Mansour");
        Assert.Equal(2, nameResults.Count);

        // Search by partial phone
        var phoneResults = await customerService.SearchCustomersAsync("1234567");
        Assert.Single(phoneResults);
        Assert.Equal("Fahad Al-Harbi", phoneResults.First().FullName);

        // Filter by status
        var activeResults = await customerService.SearchCustomersAsync("Mansour", CustomerStatus.Active);
        Assert.Single(activeResults);
        Assert.Equal("Khalid Mansour", activeResults.First().FullName);
    }

    [Fact]
    public async Task UpdateCustomerAsync_ModifiesFields_AndSetsModifiedTimestamp()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var customer = new Customer
        {
            FullName = "Initial Name",
            PhoneNumber = "0500000001",
            Email = "initial@client.internal",
            Address = "Old Address",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var customerService = new CustomerService(db, userManager, NullLogger<CustomerService>.Instance);

        var updateDto = new UpdateCustomerDto
        {
            Id = customer.Id,
            FullName = "Updated Name",
            PhoneNumber = "0500000002",
            Email = "updated@client.internal",
            Address = "New District, Jeddah",
            Status = CustomerStatus.Inactive,
            InternalNotes = "Temporarily paused."
        };

        var result = await customerService.UpdateCustomerAsync(updateDto, "staff-user-id");

        Assert.True(result.Succeeded);

        var updated = await db.Customers.FindAsync(customer.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.FullName);
        Assert.Equal("0500000002", updated.PhoneNumber);
        Assert.Equal(CustomerStatus.Inactive, updated.Status);
        Assert.False(updated.IsActive);
        Assert.NotNull(updated.ModifiedAtUtc);
    }

    [Fact]
    public async Task UpdateCustomerAsync_PreventsDuplicatePhone_WithAnotherCustomer()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var customer1 = new Customer { FullName = "Customer A", PhoneNumber = "0501111111" };
        var customer2 = new Customer { FullName = "Customer B", PhoneNumber = "0502222222" };
        db.Customers.AddRange(customer1, customer2);
        await db.SaveChangesAsync();

        var customerService = new CustomerService(db, userManager, NullLogger<CustomerService>.Instance);

        // Try updating customer 2 to use customer 1's phone number
        var updateDto = new UpdateCustomerDto
        {
            Id = customer2.Id,
            FullName = "Customer B Updated",
            PhoneNumber = "0501111111" // conflict!
        };

        var result = await customerService.UpdateCustomerAsync(updateDto, "staff-user-id");

        Assert.False(result.Succeeded);
        Assert.Contains("phone number", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ArchiveCustomerAsync_SetsArchivedStatus_AndDeactivatesLinkedIdentityAccount()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        // Create user
        var user = new ApplicationUser
        {
            UserName = "portal@client.internal",
            Email = "portal@client.internal",
            IsActive = true
        };
        await userManager.CreateAsync(user, "Password123!");

        // Create customer linked to user
        var customer = new Customer
        {
            FullName = "Active Client",
            PhoneNumber = "0509990001",
            IdentityUserId = user.Id,
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var customerService = new CustomerService(db, userManager, NullLogger<CustomerService>.Instance);

        var result = await customerService.ArchiveCustomerAsync(customer.Id, "staff-user-id");

        Assert.True(result.Succeeded);

        // Verify customer is archived
        var archivedCustomer = await db.Customers.FindAsync(customer.Id);
        Assert.Equal(CustomerStatus.Archived, archivedCustomer!.Status);
        Assert.False(archivedCustomer.IsActive);

        // Verify linked identity user account is deactivated
        var reloadedUser = await userManager.FindByIdAsync(user.Id);
        Assert.False(reloadedUser!.IsActive);
    }

    [Fact]
    public async Task RestoreCustomerAsync_RestoresStatusToActive()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var customer = new Customer
        {
            FullName = "Archived Client",
            PhoneNumber = "0507778899",
            Status = CustomerStatus.Archived
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var customerService = new CustomerService(db, userManager, NullLogger<CustomerService>.Instance);

        var result = await customerService.RestoreCustomerAsync(customer.Id, "staff-user-id");

        Assert.True(result.Succeeded);

        var restored = await db.Customers.FindAsync(customer.Id);
        Assert.Equal(CustomerStatus.Active, restored!.Status);
        Assert.True(restored.IsActive);
    }

    [Fact]
    public async Task Customer_CanBeAssociatedWithMultipleProperties()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var customer = new Customer
        {
            FullName = "VIP Landlord",
            PhoneNumber = "0503334455"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        // Associate multiple properties
        db.Properties.AddRange(
            new Property { Title = "Al-Malqa Commercial Land #12", Type = PropertyType.Land, ReferenceNumber = "PRP-T-001", Governorate = "Riyadh", City = "Riyadh", DistrictOrArea = "Test", AreaMeasurement = 100, CustomerId = customer.Id },
            new Property { Title = "Al-Nakheel Luxury Villa #4B", Type = PropertyType.Villa, ReferenceNumber = "PRP-T-002", Governorate = "Riyadh", City = "Riyadh", DistrictOrArea = "Test", AreaMeasurement = 200, CustomerId = customer.Id }
        );
        await db.SaveChangesAsync();

        var customerService = new CustomerService(db, userManager, NullLogger<CustomerService>.Instance);

        var detail = await customerService.GetCustomerByIdAsync(customer.Id);

        Assert.NotNull(detail);
        Assert.Equal(2, detail.PropertyCount);
        Assert.Contains("Al-Malqa Commercial Land #12", detail.PropertyTitles);
        Assert.Contains("Al-Nakheel Luxury Villa #4B", detail.PropertyTitles);
    }

    [Fact]
    public void CustomersController_StateChangingActions_HaveAntiForgeryToken()
    {
        var createPost = typeof(CustomersController).GetMethod("Create", [typeof(CreateCustomerDto)]);
        Assert.NotNull(createPost);
        Assert.NotNull(createPost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        var editPost = typeof(CustomersController).GetMethod("Edit", [typeof(UpdateCustomerDto)]);
        Assert.NotNull(editPost);
        Assert.NotNull(editPost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        var archivePost = typeof(CustomersController).GetMethod("Archive", [typeof(int)]);
        Assert.NotNull(archivePost);
        Assert.NotNull(archivePost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        var restorePost = typeof(CustomersController).GetMethod("Restore", [typeof(int)]);
        Assert.NotNull(restorePost);
        Assert.NotNull(restorePost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }
}
