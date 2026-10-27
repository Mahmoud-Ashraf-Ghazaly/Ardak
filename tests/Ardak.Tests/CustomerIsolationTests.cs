using System.Reflection;
using Ardak.Application.DTOs;
using Ardak.Application.Interfaces;
using Ardak.Infrastructure.Data;
using Ardak.Infrastructure.Identity;
using Ardak.Infrastructure.Services;
using Ardak.Model.Constants;
using Ardak.Model.Entities;
using Ardak.Model.Enums;
using Ardak.Tests.Helpers;
using landy.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ardak.Tests;

public class CustomerIsolationTests
{
    private static (IServiceProvider sp, ArdakDbContext db, UserManager<ApplicationUser> userManager) CreateContext(string dbName)
    {
        var sp = IdentityTestHelper.CreateTestServiceProvider(dbName);
        var db = sp.GetRequiredService<ArdakDbContext>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        return (sp, db, userManager);
    }

    [Fact]
    public async Task GetApprovedCustomerProfileAsync_ReturnsProfile_WhenCustomerAccessesOwnId()
    {
        var (sp, db, userManager) = CreateContext(nameof(GetApprovedCustomerProfileAsync_ReturnsProfile_WhenCustomerAccessesOwnId));

        var customer = new Customer
        {
            FullName = "Allowed Client",
            Email = "allowed@client.internal",
            PhoneNumber = "12345678",
            NationalIdOrTaxNumber = "TAX-999",
            InternalNotes = "Private financial notes",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.IsCustomer).Returns(true);
        mockCurrentUser.Setup(u => u.CustomerId).Returns(customer.Id);
        mockCurrentUser.Setup(u => u.UserId).Returns("customer-user-id");

        var portalService = new CustomerPortalService(db, mockCurrentUser.Object, NullLogger<CustomerPortalService>.Instance, userManager);

        var profile = await portalService.GetApprovedCustomerProfileAsync(customer.Id, "customer-user-id");

        Assert.NotNull(profile);
        Assert.Equal(customer.Id, profile.Id);
        Assert.Equal("Allowed Client", profile.FullName);
        Assert.Equal("allowed@client.internal", profile.Email);
    }

    [Fact]
    public async Task GetApprovedCustomerProfileAsync_ReturnsNull_WhenCustomerAttemptsToAccessDifferentCustomerId()
    {
        var (sp, db, userManager) = CreateContext(nameof(GetApprovedCustomerProfileAsync_ReturnsNull_WhenCustomerAttemptsToAccessDifferentCustomerId));

        var customer1 = new Customer { FullName = "Customer One", Email = "c1@test.internal", PhoneNumber = "11111" };
        var customer2 = new Customer { FullName = "Customer Two", Email = "c2@test.internal", PhoneNumber = "22222" };
        db.Customers.AddRange(customer1, customer2);
        await db.SaveChangesAsync();

        // Customer One attempts to fetch Customer Two's profile
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.IsCustomer).Returns(true);
        mockCurrentUser.Setup(u => u.CustomerId).Returns(customer1.Id);
        mockCurrentUser.Setup(u => u.UserId).Returns("c1-user-id");

        var portalService = new CustomerPortalService(db, mockCurrentUser.Object, NullLogger<CustomerPortalService>.Instance, userManager);

        var unauthorizedResult = await portalService.GetApprovedCustomerProfileAsync(customer2.Id, "c1-user-id");

        // Customer isolation must prevent access across tenant/customer boundaries
        Assert.Null(unauthorizedResult);
    }

    [Fact]
    public async Task CustomerCannotAccessAnotherCustomersProperty_DirectRequest()
    {
        var (sp, db, userManager) = CreateContext(nameof(CustomerCannotAccessAnotherCustomersProperty_DirectRequest));

        var customer1 = new Customer { FullName = "Legit Owner", PhoneNumber = "111" };
        var customer2 = new Customer { FullName = "Intruder Customer", PhoneNumber = "222" };
        db.Customers.AddRange(customer1, customer2);
        await db.SaveChangesAsync();

        var propertyOfCustomer1 = new Property
        {
            ReferenceNumber = "PRP-SECRET-01",
            Title = "Customer 1 Prime Land",
            Type = PropertyType.Land,
            Governorate = "Riyadh",
            City = "Riyadh",
            DistrictOrArea = "Al-Olaya",
            CustomerId = customer1.Id,
            InternalNotes = "Confidential acquisition deal"
        };
        db.Properties.Add(propertyOfCustomer1);
        await db.SaveChangesAsync();

        // Customer 2 attempts to directly access Customer 1's property
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.IsCustomer).Returns(true);
        mockCurrentUser.Setup(u => u.CustomerId).Returns(customer2.Id);
        mockCurrentUser.Setup(u => u.UserId).Returns("intruder-user-id");

        var portalService = new CustomerPortalService(db, mockCurrentUser.Object, NullLogger<CustomerPortalService>.Instance, userManager);

        var unauthorizedProperty = await portalService.GetCustomerPropertyDetailAsync(propertyOfCustomer1.Id, "intruder-user-id");

        // Access must be strictly denied (null)
        Assert.Null(unauthorizedProperty);

        // Controller test: Direct URL /CustomerPortal/PropertyDetails/{id} returns NotFound (safe response)
        var mockDocService = new Mock<IDocumentService>();
        var controller = new CustomerPortalController(portalService, mockDocService.Object, mockCurrentUser.Object, NullLogger<CustomerPortalController>.Instance);

        var actionResult = await controller.PropertyDetails(propertyOfCustomer1.Id);
        var notFound = Assert.IsType<NotFoundObjectResult>(actionResult);
        Assert.Contains("restricted", notFound.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CustomerCannotAccessAnotherCustomersProcedure_DirectRequest()
    {
        var (sp, db, userManager) = CreateContext(nameof(CustomerCannotAccessAnotherCustomersProcedure_DirectRequest));

        var customer1 = new Customer { FullName = "Customer A", PhoneNumber = "111" };
        var customer2 = new Customer { FullName = "Customer B", PhoneNumber = "222" };
        db.Customers.AddRange(customer1, customer2);
        await db.SaveChangesAsync();

        var prop1 = new Property { ReferenceNumber = "P1", Title = "Prop 1", CustomerId = customer1.Id };
        db.Properties.Add(prop1);
        await db.SaveChangesAsync();

        var proc1 = new Procedure
        {
            PropertyId = prop1.Id,
            Type = ProcedureType.Taqneen,
            Status = ProcedureStatus.UnderReview,
            IsCustomerVisible = true, // Even if visible to Customer 1!
            CustomerUpdateNotes = "Review in progress"
        };
        db.Procedures.Add(proc1);
        await db.SaveChangesAsync();

        // Customer 2 attempts to access procedure of Customer 1's property
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.IsCustomer).Returns(true);
        mockCurrentUser.Setup(u => u.CustomerId).Returns(customer2.Id);
        mockCurrentUser.Setup(u => u.UserId).Returns("c2-user");

        var portalService = new CustomerPortalService(db, mockCurrentUser.Object, NullLogger<CustomerPortalService>.Instance, userManager);

        var result = await portalService.GetCustomerProcedureDetailAsync(proc1.Id, "c2-user");

        // Access must be denied
        Assert.Null(result);

        // Controller check: direct URL returns safe NotFound
        var controller = new CustomerPortalController(portalService, Mock.Of<IDocumentService>(), mockCurrentUser.Object, NullLogger<CustomerPortalController>.Instance);
        var actionResult = await controller.ProcedureDetails(proc1.Id);
        Assert.IsType<NotFoundObjectResult>(actionResult);
    }

    [Fact]
    public async Task CustomerCannotAccessProcedureWhenVisibilityIsDisabled_EvenOnOwnProperty()
    {
        var (sp, db, userManager) = CreateContext(nameof(CustomerCannotAccessProcedureWhenVisibilityIsDisabled_EvenOnOwnProperty));

        var customer = new Customer { FullName = "Owner Customer", PhoneNumber = "555" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var prop = new Property { ReferenceNumber = "P-OWNED", Title = "Owned Land", CustomerId = customer.Id };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        // Procedure on own property, but IsCustomerVisible is FALSE (internal only)
        var hiddenProc = new Procedure
        {
            PropertyId = prop.Id,
            Type = ProcedureType.Tasaleh,
            Status = ProcedureStatus.Submitted,
            IsCustomerVisible = false, // Private / disabled for customer
            InternalNotes = "Do not show to customer yet until committee convenes"
        };
        db.Procedures.Add(hiddenProc);
        await db.SaveChangesAsync();

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.IsCustomer).Returns(true);
        mockCurrentUser.Setup(u => u.CustomerId).Returns(customer.Id);
        mockCurrentUser.Setup(u => u.UserId).Returns("owner-user");

        var portalService = new CustomerPortalService(db, mockCurrentUser.Object, NullLogger<CustomerPortalService>.Instance, userManager);

        var result = await portalService.GetCustomerProcedureDetailAsync(hiddenProc.Id, "owner-user");

        // Unapproved procedure must NOT be accessible to the customer
        Assert.Null(result);
    }

    [Fact]
    public async Task CustomerCannotDownloadAnotherCustomersDocument_DirectRequest()
    {
        var (sp, db, userManager) = CreateContext(nameof(CustomerCannotDownloadAnotherCustomersDocument_DirectRequest));

        var customer1 = new Customer { FullName = "Customer A", PhoneNumber = "111" };
        var customer2 = new Customer { FullName = "Customer B", PhoneNumber = "222" };
        db.Customers.AddRange(customer1, customer2);
        await db.SaveChangesAsync();

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.IsCustomer).Returns(true);
        mockCurrentUser.Setup(u => u.CustomerId).Returns(customer2.Id);
        mockCurrentUser.Setup(u => u.UserId).Returns("c2-user");

        var mockDocService = new Mock<IDocumentService>();
        mockDocService
            .Setup(s => s.GetDocumentDownloadAsync(999, "c2-user"))
            .ReturnsAsync(ServiceResult<DocumentDownloadResult>.Failure("Unauthorized to access or download this document."));

        var controller = new CustomerPortalController(Mock.Of<ICustomerPortalService>(), mockDocService.Object, mockCurrentUser.Object, NullLogger<CustomerPortalController>.Instance);

        var result = await controller.DownloadDocument(999);

        // Unauthorized direct download request must return Forbid
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CustomerCanAccessOwnApprovedRecords_WithCorrectCustomerUpdateNotes()
    {
        var (sp, db, userManager) = CreateContext(nameof(CustomerCanAccessOwnApprovedRecords_WithCorrectCustomerUpdateNotes));

        var customer = new Customer { FullName = "Legitimate Owner", PhoneNumber = "777" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var prop = new Property
        {
            ReferenceNumber = "PRP-VALID-01",
            Title = "Valid Land Plot",
            Type = PropertyType.Land,
            CustomerId = customer.Id,
            InternalNotes = "Internal secret appraisal SAR 5,000,000"
        };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var visibleProc = new Procedure
        {
            PropertyId = prop.Id,
            Type = ProcedureType.Taqneen,
            Status = ProcedureStatus.InProgress,
            IsCustomerVisible = true, // Explicitly approved
            CustomerUpdateNotes = "Survey inspection complete. Awaiting committee signoff.",
            InternalNotes = "Staff confidential fee note",
            TotalFee = 25000m,
            AmountPaid = 10000m
        };
        var hiddenProc = new Procedure
        {
            PropertyId = prop.Id,
            Type = ProcedureType.Tasaleh,
            Status = ProcedureStatus.Submitted,
            IsCustomerVisible = false // Not approved
        };
        db.Procedures.AddRange(visibleProc, hiddenProc);
        await db.SaveChangesAsync();

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.IsCustomer).Returns(true);
        mockCurrentUser.Setup(u => u.CustomerId).Returns(customer.Id);
        mockCurrentUser.Setup(u => u.UserId).Returns("owner-user");

        var portalService = new CustomerPortalService(db, mockCurrentUser.Object, NullLogger<CustomerPortalService>.Instance, userManager);

        var propertyDetail = await portalService.GetCustomerPropertyDetailAsync(prop.Id, "owner-user");

        Assert.NotNull(propertyDetail);
        Assert.Equal("PRP-VALID-01", propertyDetail.ReferenceNumber);

        // Must ONLY contain the approved procedure (1 out of 2)
        Assert.Single(propertyDetail.ApprovedProcedures);
        var approved = propertyDetail.ApprovedProcedures[0];
        Assert.Equal(visibleProc.Id, approved.Id);
        Assert.Equal("Survey inspection complete. Awaiting committee signoff.", approved.CustomerUpdateNotes);

        // Fetch single procedure detail
        var procDetail = await portalService.GetCustomerProcedureDetailAsync(visibleProc.Id, "owner-user");
        Assert.NotNull(procDetail);
        Assert.Equal(ProcedureStatus.InProgress, procDetail.Status);
        Assert.Equal("Survey inspection complete. Awaiting committee signoff.", procDetail.CustomerUpdateNotes);
    }

    [Fact]
    public async Task CoOwnerCanAccessSharedProperty_NonOwnerCannot()
    {
        var (sp, db, userManager) = CreateContext(nameof(CoOwnerCanAccessSharedProperty_NonOwnerCannot));

        var primaryOwner = new Customer { FullName = "Primary Owner", PhoneNumber = "111" };
        var coOwner = new Customer { FullName = "Co Owner", PhoneNumber = "222" };
        var nonOwner = new Customer { FullName = "Non Owner", PhoneNumber = "333" };
        db.Customers.AddRange(primaryOwner, coOwner, nonOwner);
        await db.SaveChangesAsync();

        var property = new Property
        {
            ReferenceNumber = "PRP-COOWNED",
            Title = "Shared Real Estate",
            CustomerId = primaryOwner.Id
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        // Register co-owner in PropertyCustomer
        db.PropertyCustomers.Add(new PropertyCustomer
        {
            PropertyId = property.Id,
            CustomerId = coOwner.Id,
            OwnershipRole = "Co-Owner Partner",
            OwnershipPercentage = 40m
        });
        await db.SaveChangesAsync();

        // 1. Co-owner access
        var mockCoOwnerUser = new Mock<ICurrentUserService>();
        mockCoOwnerUser.Setup(u => u.IsCustomer).Returns(true);
        mockCoOwnerUser.Setup(u => u.CustomerId).Returns(coOwner.Id);
        mockCoOwnerUser.Setup(u => u.UserId).Returns("co-user");

        var portalServiceCoOwner = new CustomerPortalService(db, mockCoOwnerUser.Object, NullLogger<CustomerPortalService>.Instance, userManager);
        var coOwnerDetail = await portalServiceCoOwner.GetCustomerPropertyDetailAsync(property.Id, "co-user");

        Assert.NotNull(coOwnerDetail);
        Assert.Equal("Co-Owner Partner", coOwnerDetail.OwnershipRole);
        Assert.Equal(40m, coOwnerDetail.OwnershipPercentage);

        // 2. Non-owner access
        var mockNonOwnerUser = new Mock<ICurrentUserService>();
        mockNonOwnerUser.Setup(u => u.IsCustomer).Returns(true);
        mockNonOwnerUser.Setup(u => u.CustomerId).Returns(nonOwner.Id);
        mockNonOwnerUser.Setup(u => u.UserId).Returns("non-owner-user");

        var portalServiceNonOwner = new CustomerPortalService(db, mockNonOwnerUser.Object, NullLogger<CustomerPortalService>.Instance, userManager);
        var nonOwnerDetail = await portalServiceNonOwner.GetCustomerPropertyDetailAsync(property.Id, "non-owner-user");

        Assert.Null(nonOwnerDetail);
    }

    [Fact]
    public void CustomerFacingDtos_NeverContainConfidentialPropertiesOrStorageKeys()
    {
        // 1. CustomerProfileDto
        var profileType = typeof(CustomerProfileDto);
        Assert.Null(profileType.GetProperty("InternalNotes"));

        // 2. CustomerPropertySummaryDto
        var propSumType = typeof(CustomerPropertySummaryDto);
        Assert.Null(propSumType.GetProperty("InternalNotes"));

        // 3. CustomerPropertyDetailDto
        var propDetailType = typeof(CustomerPropertyDetailDto);
        Assert.Null(propDetailType.GetProperty("InternalNotes"));

        // 4. CustomerProcedureDto
        var procType = typeof(CustomerProcedureDto);
        Assert.Null(procType.GetProperty("InternalNotes"));
        Assert.Null(procType.GetProperty("TotalFee"));
        Assert.Null(procType.GetProperty("AmountPaid"));
        Assert.Null(procType.GetProperty("BalanceDue"));
        Assert.Null(procType.GetProperty("MissingDocuments"));
        Assert.Null(procType.GetProperty("NextActionDeadline"));
        Assert.Null(procType.GetProperty("NextActionDescription"));

        // 5. CustomerDocumentDto
        var docType = typeof(CustomerDocumentDto);
        Assert.Null(docType.GetProperty("InternalNotes"));
        Assert.Null(docType.GetProperty("StorageKey"));
        Assert.Null(docType.GetProperty("ConfidentialityLevel"));
    }

    [Fact]
    public void CustomerPortalController_RequiresCustomerPolicy()
    {
        var authAttr = typeof(CustomerPortalController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal(AppPolicies.RequireCustomer, authAttr.Policy);
    }

    [Fact]
    public async Task CustomerPortalController_ReturnsForbid_WhenCallerHasNoCustomerIdClaim()
    {
        var mockPortalService = new Mock<ICustomerPortalService>();
        var mockDocService = new Mock<IDocumentService>();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.CustomerId).Returns((int?)null); // No customer id linked

        var controller = new CustomerPortalController(mockPortalService.Object, mockDocService.Object, mockCurrentUser.Object, NullLogger<CustomerPortalController>.Instance);

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }
}
