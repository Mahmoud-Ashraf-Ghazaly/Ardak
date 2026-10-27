using System.Reflection;
using Ardak.Application.DTOs;
using Ardak.Infrastructure.Data;
using Ardak.Infrastructure.Services;
using Ardak.Model.Entities;
using Ardak.Model.Enums;
using Ardak.Tests.Helpers;
using landy.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ardak.Tests;

public class PropertyManagementTests
{
    [Fact]
    public async Task CreatePropertyAsync_Succeeds_WithCompleteProfileAndAuditTimestamps()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();

        var customer = new Customer
        {
            FullName = "Sheikh Mohammed Al-Otaibi",
            PhoneNumber = "0501112233",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var propertyService = new PropertyService(db, NullLogger<PropertyService>.Instance);

        var dto = new CreatePropertyDto
        {
            ReferenceNumber = "PRP-2026-0001",
            Type = PropertyType.Land,
            Title = "Al-Malqa Commercial Plot 142",
            Description = "Corner commercial plot with road frontage.",
            Governorate = "Riyadh Province",
            City = "Riyadh",
            DistrictOrArea = "Al-Malqa",
            AddressDetails = "Corner of Prince Turki Rd and Anas Ibn Malik Rd",
            AreaMeasurement = 1250.75m,
            AreaUnit = "sqm",
            Latitude = 24.812345,
            Longitude = 46.612345,
            OwnershipType = "Electronic Deed",
            DeedOrTitleNumber = "SUK-3100998877",
            PrimaryCustomerId = customer.Id,
            InternalNotes = "Private financial assessment: Estimated value SAR 12,000,000."
        };

        var result = await propertyService.CreatePropertyAsync(dto, "staff-user-id");

        Assert.True(result.Succeeded);
        Assert.True(result.Data > 0);

        var property = await propertyService.GetPropertyByIdAsync(result.Data);
        Assert.NotNull(property);
        Assert.Equal("PRP-2026-0001", property.ReferenceNumber);
        Assert.Equal(PropertyType.Land, property.Type);
        Assert.Equal("Al-Malqa Commercial Plot 142", property.Title);
        Assert.Equal("Riyadh", property.City);
        Assert.Equal(1250.75m, property.AreaMeasurement);
        Assert.Equal(24.812345, property.Latitude);
        Assert.Equal(PropertyStatus.Active, property.Status);
        Assert.Equal(customer.Id, property.PrimaryCustomerId);
        Assert.Equal("Sheikh Mohammed Al-Otaibi", property.PrimaryCustomerName);
        Assert.Equal("Private financial assessment: Estimated value SAR 12,000,000.", property.InternalNotes);
        Assert.True(property.CreatedAtUtc <= DateTime.UtcNow);
        Assert.Null(property.ModifiedAtUtc);
    }

    [Fact]
    public async Task CreatePropertyAsync_PreventsDuplicateReferenceNumber()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();

        var customer = new Customer { FullName = "Owner Client", PhoneNumber = "0509998877" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var propertyService = new PropertyService(db, NullLogger<PropertyService>.Instance);

        var dto1 = new CreatePropertyDto
        {
            ReferenceNumber = "REF-DUPLICATE-01",
            Type = PropertyType.Land,
            Title = "Original Plot",
            Governorate = "Riyadh",
            City = "Riyadh",
            DistrictOrArea = "Al-Olaya",
            AreaMeasurement = 500,
            PrimaryCustomerId = customer.Id
        };
        var firstResult = await propertyService.CreatePropertyAsync(dto1, "staff-1");
        Assert.True(firstResult.Succeeded);

        // Attempt second property with same reference number
        var dto2 = new CreatePropertyDto
        {
            ReferenceNumber = "REF-DUPLICATE-01", // duplicate!
            Type = PropertyType.Villa,
            Title = "Duplicate Villa",
            Governorate = "Riyadh",
            City = "Riyadh",
            DistrictOrArea = "Al-Olaya",
            AreaMeasurement = 600,
            PrimaryCustomerId = customer.Id
        };
        var secondResult = await propertyService.CreatePropertyAsync(dto2, "staff-1");

        Assert.False(secondResult.Succeeded);
        Assert.Contains("already exists", secondResult.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePropertyAsync_Fails_WhenPrimaryCustomerDoesNotExist()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();

        var propertyService = new PropertyService(db, NullLogger<PropertyService>.Instance);

        var dto = new CreatePropertyDto
        {
            ReferenceNumber = "PRP-NOCUST-01",
            Type = PropertyType.Land,
            Title = "Orphan Plot",
            Governorate = "Riyadh",
            City = "Riyadh",
            DistrictOrArea = "Al-Malqa",
            AreaMeasurement = 400,
            PrimaryCustomerId = 999999 // Non-existent customer
        };

        var result = await propertyService.CreatePropertyAsync(dto, "staff-1");

        Assert.False(result.Succeeded);
        Assert.Contains("customer record does not exist", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchPropertiesAsync_FiltersByReference_Location_AndType()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();

        var customer = new Customer { FullName = "Search Test Client", PhoneNumber = "0551234567" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.Properties.AddRange(
            new Property
            {
                ReferenceNumber = "PROP-JED-01",
                Title = "Al-Shati Coastal Villa",
                Type = PropertyType.Villa,
                Governorate = "Makkah Province",
                City = "Jeddah",
                DistrictOrArea = "Al-Shati",
                AreaMeasurement = 900,
                CustomerId = customer.Id,
                Status = PropertyStatus.Active
            },
            new Property
            {
                ReferenceNumber = "PROP-RUH-02",
                Title = "Al-Narjis Residential Land",
                Type = PropertyType.Land,
                Governorate = "Riyadh Province",
                City = "Riyadh",
                DistrictOrArea = "Al-Narjis",
                AreaMeasurement = 750,
                CustomerId = customer.Id,
                Status = PropertyStatus.Active
            },
            new Property
            {
                ReferenceNumber = "PROP-RUH-03",
                Title = "Hittin Prime Villa",
                Type = PropertyType.Villa,
                Governorate = "Riyadh Province",
                City = "Riyadh",
                DistrictOrArea = "Hittin",
                AreaMeasurement = 650,
                CustomerId = customer.Id,
                Status = PropertyStatus.Reserved
            }
        );
        await db.SaveChangesAsync();

        var propertyService = new PropertyService(db, NullLogger<PropertyService>.Instance);

        // Search by location (Jeddah)
        var jeddahResults = await propertyService.SearchPropertiesAsync(new PropertySearchFilterDto { Location = "Jeddah" });
        Assert.Single(jeddahResults);
        Assert.Equal("PROP-JED-01", jeddahResults.First().ReferenceNumber);

        // Filter by Type (Villa) in Riyadh
        var riyadhVillas = await propertyService.SearchPropertiesAsync(new PropertySearchFilterDto { Location = "Riyadh", Type = PropertyType.Villa });
        Assert.Single(riyadhVillas);
        Assert.Equal("PROP-RUH-03", riyadhVillas.First().ReferenceNumber);

        // Filter by Reference number
        var refResults = await propertyService.SearchPropertiesAsync(new PropertySearchFilterDto { ReferenceNumber = "RUH-02" });
        Assert.Single(refResults);
        Assert.Equal("PROP-RUH-02", refResults.First().ReferenceNumber);

        // Filter by Status (Reserved)
        var reservedResults = await propertyService.SearchPropertiesAsync(new PropertySearchFilterDto { Status = PropertyStatus.Reserved });
        Assert.Single(reservedResults);
        Assert.Equal("PROP-RUH-03", reservedResults.First().ReferenceNumber);
    }

    [Fact]
    public async Task UpdatePropertyAsync_ModifiesFields_AndSetsModifiedTimestamp()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();

        var customer = new Customer { FullName = "Update Owner", PhoneNumber = "0502223344" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var property = new Property
        {
            ReferenceNumber = "PRP-UPDATE-01",
            Title = "Original Name",
            Type = PropertyType.Land,
            Governorate = "Riyadh",
            City = "Riyadh",
            DistrictOrArea = "Al-Yasmin",
            AreaMeasurement = 800,
            CustomerId = customer.Id,
            Status = PropertyStatus.Active
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var propertyService = new PropertyService(db, NullLogger<PropertyService>.Instance);

        var updateDto = new UpdatePropertyDto
        {
            Id = property.Id,
            ReferenceNumber = "PRP-UPDATE-01",
            Title = "Updated Land Profile",
            Type = PropertyType.Land,
            Governorate = "Riyadh",
            City = "Riyadh",
            DistrictOrArea = "Al-Yasmin North",
            AreaMeasurement = 850.50m,
            AreaUnit = "sqm",
            Status = PropertyStatus.UnderReview,
            PrimaryCustomerId = customer.Id,
            InternalNotes = "Updated valuation after boundary adjustment."
        };

        var result = await propertyService.UpdatePropertyAsync(updateDto, "staff-user-id");

        Assert.True(result.Succeeded);

        var updated = await db.Properties.FindAsync(property.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Land Profile", updated.Title);
        Assert.Equal(850.50m, updated.AreaMeasurement);
        Assert.Equal(PropertyStatus.UnderReview, updated.Status);
        Assert.NotNull(updated.ModifiedAtUtc);
    }

    [Fact]
    public async Task MultiplePropertiesPerCustomer_AreAssociatedCorrectly()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();

        var customer = new Customer { FullName = "Portfolio Investor", PhoneNumber = "0507770011" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var propertyService = new PropertyService(db, NullLogger<PropertyService>.Instance);

        await propertyService.CreatePropertyAsync(new CreatePropertyDto
        {
            ReferenceNumber = "PORT-01",
            Title = "Investment Plot A",
            Type = PropertyType.Land,
            Governorate = "Riyadh",
            City = "Riyadh",
            DistrictOrArea = "Al-Malqa",
            AreaMeasurement = 1000,
            PrimaryCustomerId = customer.Id
        }, "staff-1");

        await propertyService.CreatePropertyAsync(new CreatePropertyDto
        {
            ReferenceNumber = "PORT-02",
            Title = "Investment Plot B",
            Type = PropertyType.Land,
            Governorate = "Riyadh",
            City = "Riyadh",
            DistrictOrArea = "Al-Narjis",
            AreaMeasurement = 1200,
            PrimaryCustomerId = customer.Id
        }, "staff-1");

        var customerProperties = await propertyService.SearchPropertiesAsync(new PropertySearchFilterDto
        {
            CustomerId = customer.Id
        });

        Assert.Equal(2, customerProperties.Count);
        Assert.Contains(customerProperties, p => p.ReferenceNumber == "PORT-01");
        Assert.Contains(customerProperties, p => p.ReferenceNumber == "PORT-02");
    }

    [Fact]
    public async Task MultipleCustomersPerProperty_SupportsCoOwnersAndPercentages()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();

        var primaryCustomer = new Customer { FullName = "Lead Partner", PhoneNumber = "0501110001" };
        var coOwner1 = new Customer { FullName = "Second Partner", PhoneNumber = "0502220002" };
        var coOwner2 = new Customer { FullName = "Third Partner", PhoneNumber = "0503330003" };
        db.Customers.AddRange(primaryCustomer, coOwner1, coOwner2);
        await db.SaveChangesAsync();

        var propertyService = new PropertyService(db, NullLogger<PropertyService>.Instance);

        var createResult = await propertyService.CreatePropertyAsync(new CreatePropertyDto
        {
            ReferenceNumber = "SHARED-PROP-01",
            Title = "Joint Commercial Complex",
            Type = PropertyType.Land,
            Governorate = "Riyadh",
            City = "Riyadh",
            DistrictOrArea = "King Abdullah Financial District",
            AreaMeasurement = 5000,
            PrimaryCustomerId = primaryCustomer.Id
        }, "staff-1");

        Assert.True(createResult.Succeeded);
        var propertyId = createResult.Data;

        // Associate co-owners with percentages
        var assoc1 = await propertyService.AssociateCustomerAsync(propertyId, coOwner1.Id, "Co-Owner", 30.00m, "staff-1");
        var assoc2 = await propertyService.AssociateCustomerAsync(propertyId, coOwner2.Id, "Silent Partner", 20.00m, "staff-1");

        Assert.True(assoc1.Succeeded);
        Assert.True(assoc2.Succeeded);

        var propertyDetail = await propertyService.GetPropertyByIdAsync(propertyId);
        Assert.NotNull(propertyDetail);
        Assert.Equal(3, propertyDetail.AssociatedCustomers.Count);
        Assert.Contains(propertyDetail.AssociatedCustomers, ac => ac.CustomerId == coOwner1.Id && ac.OwnershipPercentage == 30.00m);
        Assert.Contains(propertyDetail.AssociatedCustomers, ac => ac.CustomerId == coOwner2.Id && ac.OwnershipRole == "Silent Partner");
    }

    [Fact]
    public async Task RemoveCustomerAssociationAsync_RemovesNonPrimaryCoOwner_AndProtectsPrimaryOwner()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();

        var primaryCustomer = new Customer { FullName = "Primary Customer", PhoneNumber = "0504445555" };
        var coOwner = new Customer { FullName = "CoOwner To Remove", PhoneNumber = "0506667777" };
        db.Customers.AddRange(primaryCustomer, coOwner);
        await db.SaveChangesAsync();

        var propertyService = new PropertyService(db, NullLogger<PropertyService>.Instance);

        var createResult = await propertyService.CreatePropertyAsync(new CreatePropertyDto
        {
            ReferenceNumber = "COOWN-REM-01",
            Title = "Test Property",
            Governorate = "Riyadh",
            City = "Riyadh",
            DistrictOrArea = "Al-Olaya",
            AreaMeasurement = 500,
            PrimaryCustomerId = primaryCustomer.Id
        }, "staff-1");

        var propertyId = createResult.Data;
        await propertyService.AssociateCustomerAsync(propertyId, coOwner.Id, "Co-Owner", 25.00m, "staff-1");

        // Attempt to remove primary customer -> should fail!
        var removePrimaryResult = await propertyService.RemoveCustomerAssociationAsync(propertyId, primaryCustomer.Id, "staff-1");
        Assert.False(removePrimaryResult.Succeeded);
        Assert.Contains("primary", removePrimaryResult.Error, StringComparison.OrdinalIgnoreCase);

        // Remove non-primary co-owner -> should succeed
        var removeCoOwnerResult = await propertyService.RemoveCustomerAssociationAsync(propertyId, coOwner.Id, "staff-1");
        Assert.True(removeCoOwnerResult.Succeeded);

        var detail = await propertyService.GetPropertyByIdAsync(propertyId);
        Assert.NotNull(detail);
        Assert.DoesNotContain(detail.AssociatedCustomers, ac => ac.CustomerId == coOwner.Id);
    }

    [Fact]
    public async Task ArchiveAndRestore_UpdatesPropertyStatusCorrectly()
    {
        var services = IdentityTestHelper.CreateTestServiceProvider(Guid.NewGuid().ToString());
        var db = services.GetRequiredService<ArdakDbContext>();

        var customer = new Customer { FullName = "Archive Client", PhoneNumber = "0508889900" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var propertyService = new PropertyService(db, NullLogger<PropertyService>.Instance);

        var createResult = await propertyService.CreatePropertyAsync(new CreatePropertyDto
        {
            ReferenceNumber = "ARCH-TEST-01",
            Title = "Old Warehouse Land",
            Governorate = "Riyadh",
            City = "Riyadh",
            DistrictOrArea = "Al-Sulay",
            AreaMeasurement = 2000,
            PrimaryCustomerId = customer.Id
        }, "staff-1");

        var propertyId = createResult.Data;

        // Archive
        var archiveResult = await propertyService.ArchivePropertyAsync(propertyId, "staff-1");
        Assert.True(archiveResult.Succeeded);

        var archived = await propertyService.GetPropertyByIdAsync(propertyId);
        Assert.NotNull(archived);
        Assert.Equal(PropertyStatus.Archived, archived.Status);

        // Restore
        var restoreResult = await propertyService.RestorePropertyAsync(propertyId, "staff-1");
        Assert.True(restoreResult.Succeeded);

        var restored = await propertyService.GetPropertyByIdAsync(propertyId);
        Assert.NotNull(restored);
        Assert.Equal(PropertyStatus.Active, restored.Status);
    }

    [Fact]
    public void PropertiesController_StateChangingActions_HaveAntiForgeryToken()
    {
        var createPost = typeof(PropertiesController).GetMethod("Create", [typeof(CreatePropertyDto)]);
        Assert.NotNull(createPost);
        Assert.NotNull(createPost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        var editPost = typeof(PropertiesController).GetMethod("Edit", [typeof(UpdatePropertyDto)]);
        Assert.NotNull(editPost);
        Assert.NotNull(editPost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        var archivePost = typeof(PropertiesController).GetMethod("Archive", [typeof(int)]);
        Assert.NotNull(archivePost);
        Assert.NotNull(archivePost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        var restorePost = typeof(PropertiesController).GetMethod("Restore", [typeof(int)]);
        Assert.NotNull(restorePost);
        Assert.NotNull(restorePost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        var associatePost = typeof(PropertiesController).GetMethod("AssociateCustomer", [typeof(int), typeof(int), typeof(string), typeof(decimal?)]);
        Assert.NotNull(associatePost);
        Assert.NotNull(associatePost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        var removeAssocPost = typeof(PropertiesController).GetMethod("RemoveCustomerAssociation", [typeof(int), typeof(int)]);
        Assert.NotNull(removeAssocPost);
        Assert.NotNull(removeAssocPost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }
}
