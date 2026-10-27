using System.Reflection;
using Ardak.Application.DTOs;
using Ardak.Application.Interfaces;
using Ardak.Infrastructure.Data;
using Ardak.Infrastructure.Services;
using Ardak.Model.Constants;
using Ardak.Model.Entities;
using Ardak.Model.Enums;
using Ardak.Tests.Helpers;
using landy.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ardak.Tests;

public class AdminDashboardTests
{
    private static ArdakDbContext CreateDb(string name)
    {
        var sp = IdentityTestHelper.CreateTestServiceProvider(name);
        return sp.GetRequiredService<ArdakDbContext>();
    }

    [Fact]
    public async Task DashboardMetrics_AggregatesCountsAndGroupingsAccurately()
    {
        var db = CreateDb(nameof(DashboardMetrics_AggregatesCountsAndGroupingsAccurately));
        var service = new DashboardService(db, NullLogger<DashboardService>.Instance);

        // Seed Customers: 2 Active, 1 Inactive
        var c1 = new Customer { FullName = "Active 1", PhoneNumber = "111", Status = CustomerStatus.Active };
        var c2 = new Customer { FullName = "Active 2", PhoneNumber = "222", Status = CustomerStatus.Active };
        var c3 = new Customer { FullName = "Inactive 1", PhoneNumber = "333", Status = CustomerStatus.Inactive };
        db.Customers.AddRange(c1, c2, c3);
        await db.SaveChangesAsync();

        // Seed Properties: 2 Land (1 Active, 1 Archived), 1 Villa (Active)
        var p1 = new Property { ReferenceNumber = "P-01", Title = "Land A", Type = PropertyType.Land, Status = PropertyStatus.Active, CustomerId = c1.Id };
        var p2 = new Property { ReferenceNumber = "P-02", Title = "Land B", Type = PropertyType.Land, Status = PropertyStatus.Archived, CustomerId = c1.Id };
        var p3 = new Property { ReferenceNumber = "P-03", Title = "Villa A", Type = PropertyType.Villa, Status = PropertyStatus.Active, CustomerId = c2.Id };
        db.Properties.AddRange(p1, p2, p3);
        await db.SaveChangesAsync();

        // Seed Procedures
        // 1: Taqneen, Submitted (Pending), has missing docs
        var proc1 = new Procedure
        {
            PropertyId = p1.Id,
            Type = ProcedureType.Taqneen,
            Status = ProcedureStatus.Submitted,
            MissingDocuments = "Survey map",
            NextActionDeadline = DateTime.UtcNow.AddDays(5)
        };
        // 2: Tasaleh, InProgress (Pending), Overdue deadline
        var proc2 = new Procedure
        {
            PropertyId = p1.Id,
            Type = ProcedureType.Tasaleh,
            Status = ProcedureStatus.InProgress,
            NextActionDeadline = DateTime.UtcNow.AddDays(-2) // Overdue
        };
        // 3: UtilityConnection, Completed (Not pending)
        var proc3 = new Procedure
        {
            PropertyId = p3.Id,
            Type = ProcedureType.UtilityConnection,
            Status = ProcedureStatus.Completed
        };
        db.Procedures.AddRange(proc1, proc2, proc3);
        await db.SaveChangesAsync();

        // Seed Status History
        db.ProcedureStatusHistories.Add(new ProcedureStatusHistory
        {
            ProcedureId = proc1.Id,
            FromStatus = ProcedureStatus.Submitted,
            ToStatus = ProcedureStatus.InProgress,
            ChangeNote = "Authority acknowledged receipt",
            ChangedByUserId = "staff-1",
            ChangedAtUtc = DateTime.UtcNow
        });

        // Seed Documents: 2 active, 1 archived
        db.Documents.Add(new Document
        {
            PropertyId = p1.Id,
            Title = "Deed",
            OriginalFileName = "deed.pdf",
            StorageKey = "key1.bin",
            ContentType = "application/pdf",
            IsArchived = false
        });
        db.Documents.Add(new Document
        {
            PropertyId = p3.Id,
            Title = "Old Deed",
            OriginalFileName = "old.pdf",
            StorageKey = "key2.bin",
            ContentType = "application/pdf",
            IsArchived = true
        });
        await db.SaveChangesAsync();

        // Execute Dashboard Service
        var metrics = await service.GetDashboardMetricsAsync();

        // Assert Customer Counts
        Assert.Equal(3, metrics.TotalCustomers);
        Assert.Equal(2, metrics.ActiveCustomers);

        // Assert Property Counts & Groupings
        Assert.Equal(3, metrics.TotalProperties);
        Assert.Equal(2, metrics.ActiveProperties);
        Assert.Equal(2, metrics.PropertiesByType["Land"]);
        Assert.Equal(1, metrics.PropertiesByType["Villa"]);
        Assert.Equal(2, metrics.PropertiesByStatus["Active"]);
        Assert.Equal(1, metrics.PropertiesByStatus["Archived"]);

        // Assert Procedure Counts & Groupings
        Assert.Equal(3, metrics.TotalProcedures);
        Assert.Equal(2, metrics.PendingProceduresCount); // proc1 (Submitted) + proc2 (InProgress)
        Assert.Equal(1, metrics.ProceduresWithMissingDocsCount); // proc1
        Assert.Equal(1, metrics.OverdueDeadlinesCount); // proc2
        Assert.Equal(1, metrics.ProceduresByStatus["Submitted"]);
        Assert.Equal(1, metrics.ProceduresByStatus["InProgress"]);
        Assert.Equal(1, metrics.ProceduresByStatus["Completed"]);

        // Assert Document Counts
        Assert.Equal(1, metrics.TotalDocuments); // Only active (non-archived)

        // Assert Actionable Lists
        Assert.Equal(2, metrics.PendingProcedures.Count);
        Assert.Single(metrics.ProceduresWithMissingDocuments);
        Assert.Equal(proc1.Id, metrics.ProceduresWithMissingDocuments[0].Id);

        // Assert Recent Activity
        Assert.NotEmpty(metrics.RecentActivities);
    }

    [Fact]
    public void InternalDashboardController_RequiresAdminOrSuperAdminPolicy()
    {
        var authAttr = typeof(InternalDashboardController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal(AppPolicies.RequireAdminOrSuperAdmin, authAttr.Policy);
    }

    [Fact]
    public async Task InternalDashboardController_Index_ReturnsViewWithAdminDashboardDto()
    {
        var mockDashboardService = new Mock<IDashboardService>();
        var mockCurrentUserService = new Mock<ICurrentUserService>();

        var expectedDto = new AdminDashboardDto
        {
            TotalCustomers = 10,
            TotalProperties = 25,
            TotalProcedures = 15
        };

        mockDashboardService
            .Setup(s => s.GetDashboardMetricsAsync(It.IsAny<int>()))
            .ReturnsAsync(expectedDto);

        mockCurrentUserService.SetupGet(u => u.IsSuperAdmin).Returns(true);
        mockCurrentUserService.SetupGet(u => u.Email).Returns("super@ardak.internal");
        mockCurrentUserService.SetupGet(u => u.UserId).Returns("super-user-id");

        var controller = new InternalDashboardController(
            mockDashboardService.Object,
            mockCurrentUserService.Object,
            NullLogger<InternalDashboardController>.Instance);

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminDashboardDto>(viewResult.Model);
        Assert.Equal(10, model.TotalCustomers);
        Assert.Equal(25, model.TotalProperties);
        Assert.Equal(15, model.TotalProcedures);
        Assert.True(controller.ViewBag.IsSuperAdmin);
        Assert.Equal("super@ardak.internal", controller.ViewBag.UserEmail);
    }
}
