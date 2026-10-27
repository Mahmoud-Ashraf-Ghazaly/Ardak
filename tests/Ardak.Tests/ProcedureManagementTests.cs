using System.Reflection;
using Ardak.Application.DTOs;
using Ardak.Infrastructure.Data;
using Ardak.Infrastructure.Services;
using Ardak.Model.Constants;
using Ardak.Model.Entities;
using Ardak.Model.Enums;
using Ardak.Tests.Helpers;
using landy.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ardak.Tests;

/// <summary>
/// Tests for the ProcedureService covering creation, update, status history, and filtering.
/// Uses in-memory EF Core database — no real database required.
/// </summary>
public class ProcedureManagementTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ArdakDbContext CreateDb(string name)
    {
        var sp = IdentityTestHelper.CreateTestServiceProvider(name);
        return sp.GetRequiredService<ArdakDbContext>();
    }

    private static async Task<(ArdakDbContext db, Property property)> SeedPropertyAsync(string dbName)
    {
        var db = CreateDb(dbName);

        var customer = new Customer
        {
            FullName = "Test Customer",
            PhoneNumber = "0500000000"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var property = new Property
        {
            ReferenceNumber = "PRP-TEST-001",
            Title = "Test Land Plot",
            Type = PropertyType.Land,
            Governorate = "Riyadh",
            City = "Riyadh",
            DistrictOrArea = "Al-Malqa",
            AreaMeasurement = 500,
            CustomerId = customer.Id
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        return (db, property);
    }

    // ── Create ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProcedure_ValidInput_ShouldSucceedAndReturnId()
    {
        var (db, property) = await SeedPropertyAsync(nameof(CreateProcedure_ValidInput_ShouldSucceedAndReturnId));
        var service = new ProcedureService(db, NullLogger<ProcedureService>.Instance);

        var dto = new CreateProcedureDto
        {
            PropertyId = property.Id,
            Type = ProcedureType.Taqneen,
            Status = ProcedureStatus.Submitted,
            ApplicationNumber = "APP-001",
            ResponsibleAuthority = "General Commission",
            SubmissionDate = DateTime.Today,
            TotalFee = 5000m,
            AmountPaid = 2000m
        };

        var result = await service.CreateProcedureAsync(dto, "admin-user-id");

        Assert.True(result.Succeeded);
        Assert.True(result.Data > 0);

        var saved = await db.Procedures.FindAsync(result.Data);
        Assert.NotNull(saved);
        Assert.Equal(ProcedureType.Taqneen, saved.Type);
        Assert.Equal(ProcedureStatus.Submitted, saved.Status);
        Assert.Equal("APP-001", saved.ApplicationNumber);
        Assert.Equal(3000m, saved.BalanceDue); // 5000 - 2000
    }

    [Fact]
    public async Task CreateProcedure_NonexistentProperty_ShouldFail()
    {
        var db = CreateDb(nameof(CreateProcedure_NonexistentProperty_ShouldFail));
        var service = new ProcedureService(db, NullLogger<ProcedureService>.Instance);

        var dto = new CreateProcedureDto
        {
            PropertyId = 9999,
            Type = ProcedureType.Other,
            Status = ProcedureStatus.Submitted
        };

        var result = await service.CreateProcedureAsync(dto, "admin-user-id");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
    }

    // ── Balance Calculation ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateProcedure_NoFee_BalanceDueShouldBeNull()
    {
        var (db, property) = await SeedPropertyAsync(nameof(CreateProcedure_NoFee_BalanceDueShouldBeNull));
        var service = new ProcedureService(db, NullLogger<ProcedureService>.Instance);

        var result = await service.CreateProcedureAsync(new CreateProcedureDto
        {
            PropertyId = property.Id,
            Type = ProcedureType.UtilityConnection,
            Status = ProcedureStatus.InProgress
        }, "staff");

        Assert.True(result.Succeeded);
        var saved = await db.Procedures.FindAsync(result.Data);
        Assert.Null(saved!.BalanceDue);
    }

    // ── Update & Status History ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateProcedure_StatusChange_ShouldWriteHistoryRecord()
    {
        var (db, property) = await SeedPropertyAsync(nameof(UpdateProcedure_StatusChange_ShouldWriteHistoryRecord));
        var service = new ProcedureService(db, NullLogger<ProcedureService>.Instance);

        // Create
        var createResult = await service.CreateProcedureAsync(new CreateProcedureDto
        {
            PropertyId = property.Id,
            Type = ProcedureType.Tasaleh,
            Status = ProcedureStatus.Submitted
        }, "staff-1");

        Assert.True(createResult.Succeeded);
        int procId = createResult.Data;

        // Update with a different status
        var updateResult = await service.UpdateProcedureAsync(new UpdateProcedureDto
        {
            Id = procId,
            PropertyId = property.Id,
            Type = ProcedureType.Tasaleh,
            Status = ProcedureStatus.InProgress,
            StatusChangeNote = "Authority confirmed receipt of application."
        }, "staff-2");

        Assert.True(updateResult.Succeeded);

        var history = await db.ProcedureStatusHistories
            .Where(h => h.ProcedureId == procId)
            .ToListAsync();

        Assert.Single(history);
        Assert.Equal(ProcedureStatus.Submitted, history[0].FromStatus);
        Assert.Equal(ProcedureStatus.InProgress, history[0].ToStatus);
        Assert.Equal("Authority confirmed receipt of application.", history[0].ChangeNote);
        Assert.Equal("staff-2", history[0].ChangedByUserId);
    }

    [Fact]
    public async Task UpdateProcedure_SameStatus_ShouldNotWriteHistoryRecord()
    {
        var (db, property) = await SeedPropertyAsync(nameof(UpdateProcedure_SameStatus_ShouldNotWriteHistoryRecord));
        var service = new ProcedureService(db, NullLogger<ProcedureService>.Instance);

        var createResult = await service.CreateProcedureAsync(new CreateProcedureDto
        {
            PropertyId = property.Id,
            Type = ProcedureType.TitleDeedIssuance,
            Status = ProcedureStatus.UnderReview
        }, "staff");

        int procId = createResult.Data;

        // Update without changing status
        await service.UpdateProcedureAsync(new UpdateProcedureDto
        {
            Id = procId,
            PropertyId = property.Id,
            Type = ProcedureType.TitleDeedIssuance,
            Status = ProcedureStatus.UnderReview, // same status
            NextActionDescription = "Follow up in 2 weeks."
        }, "staff");

        var historyCount = await db.ProcedureStatusHistories.CountAsync(h => h.ProcedureId == procId);
        Assert.Equal(0, historyCount);
    }

    [Fact]
    public async Task UpdateProcedure_RecalculatesBalanceDue()
    {
        var (db, property) = await SeedPropertyAsync(nameof(UpdateProcedure_RecalculatesBalanceDue));
        var service = new ProcedureService(db, NullLogger<ProcedureService>.Instance);

        var createResult = await service.CreateProcedureAsync(new CreateProcedureDto
        {
            PropertyId = property.Id,
            Type = ProcedureType.Subdivision,
            Status = ProcedureStatus.Submitted,
            TotalFee = 10000m,
            AmountPaid = 0m
        }, "staff");

        int procId = createResult.Data;

        await service.UpdateProcedureAsync(new UpdateProcedureDto
        {
            Id = procId,
            PropertyId = property.Id,
            Type = ProcedureType.Subdivision,
            Status = ProcedureStatus.Submitted,
            TotalFee = 10000m,
            AmountPaid = 6000m
        }, "staff");

        var updated = await db.Procedures.FindAsync(procId);
        Assert.Equal(4000m, updated!.BalanceDue);
    }

    // ── Search & Filter ──────────────────────────────────────────────────────

    [Fact]
    public async Task SearchProcedures_ByPropertyId_ReturnsOnlyThatProperty()
    {
        var (db, property) = await SeedPropertyAsync(nameof(SearchProcedures_ByPropertyId_ReturnsOnlyThatProperty));
        var service = new ProcedureService(db, NullLogger<ProcedureService>.Instance);

        // Another property
        var other = new Property
        {
            ReferenceNumber = "PRP-OTHER-001",
            Title = "Other Property",
            Type = PropertyType.Villa,
            Governorate = "Jeddah",
            City = "Jeddah",
            DistrictOrArea = "Al-Rawdah",
            AreaMeasurement = 300,
            CustomerId = property.CustomerId
        };
        db.Properties.Add(other);
        await db.SaveChangesAsync();

        await service.CreateProcedureAsync(new CreateProcedureDto { PropertyId = property.Id, Type = ProcedureType.Taqneen, Status = ProcedureStatus.Submitted }, "staff");
        await service.CreateProcedureAsync(new CreateProcedureDto { PropertyId = other.Id, Type = ProcedureType.Tasaleh, Status = ProcedureStatus.Submitted }, "staff");

        var results = await service.GetProceduresByPropertyAsync(property.Id);

        Assert.Single(results);
        Assert.Equal(property.Id, results[0].PropertyId);
    }

    [Fact]
    public async Task SearchProcedures_ByStatus_FiltersCorrectly()
    {
        var (db, property) = await SeedPropertyAsync(nameof(SearchProcedures_ByStatus_FiltersCorrectly));
        var service = new ProcedureService(db, NullLogger<ProcedureService>.Instance);

        await service.CreateProcedureAsync(new CreateProcedureDto { PropertyId = property.Id, Type = ProcedureType.Taqneen, Status = ProcedureStatus.Submitted }, "staff");
        await service.CreateProcedureAsync(new CreateProcedureDto { PropertyId = property.Id, Type = ProcedureType.Tasaleh, Status = ProcedureStatus.Approved }, "staff");
        await service.CreateProcedureAsync(new CreateProcedureDto { PropertyId = property.Id, Type = ProcedureType.UtilityConnection, Status = ProcedureStatus.Approved }, "staff");

        var approvedOnly = await service.SearchProceduresAsync(new ProcedureSearchFilterDto { Status = ProcedureStatus.Approved });

        Assert.Equal(2, approvedOnly.Count);
        Assert.All(approvedOnly, p => Assert.Equal(ProcedureStatus.Approved, p.Status));
    }

    // ── GetById ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProcedureById_ExistingId_ReturnsDetailWithHistory()
    {
        var (db, property) = await SeedPropertyAsync(nameof(GetProcedureById_ExistingId_ReturnsDetailWithHistory));
        var service = new ProcedureService(db, NullLogger<ProcedureService>.Instance);

        var createResult = await service.CreateProcedureAsync(new CreateProcedureDto
        {
            PropertyId = property.Id,
            Type = ProcedureType.BoundaryDemarcation,
            Status = ProcedureStatus.Submitted,
            InternalNotes = "Staff-only note",
            MissingDocuments = "Survey certificate"
        }, "staff");

        // Change status to add history
        await service.UpdateProcedureAsync(new UpdateProcedureDto
        {
            Id = createResult.Data,
            PropertyId = property.Id,
            Type = ProcedureType.BoundaryDemarcation,
            Status = ProcedureStatus.InProgress,
            InternalNotes = "Staff-only note",
            MissingDocuments = "Survey certificate",
            StatusChangeNote = "Documents received."
        }, "staff");

        var detail = await service.GetProcedureByIdAsync(createResult.Data);

        Assert.NotNull(detail);
        Assert.Equal("Staff-only note", detail.InternalNotes);
        Assert.Equal("Survey certificate", detail.MissingDocuments);
        Assert.Single(detail.StatusHistory);
    }

    [Fact]
    public async Task GetProcedureById_NonexistentId_ReturnsNull()
    {
        var db = CreateDb(nameof(GetProcedureById_NonexistentId_ReturnsNull));
        var service = new ProcedureService(db, NullLogger<ProcedureService>.Instance);

        var detail = await service.GetProcedureByIdAsync(9999);
        Assert.Null(detail);
    }

    // ── Confidentiality check ─────────────────────────────────────────────────

    [Fact]
    public async Task SearchProcedures_SummaryDto_DoesNotExposeInternalNotesOrFinancials()
    {
        var (db, property) = await SeedPropertyAsync(nameof(SearchProcedures_SummaryDto_DoesNotExposeInternalNotesOrFinancials));
        var service = new ProcedureService(db, NullLogger<ProcedureService>.Instance);

        await service.CreateProcedureAsync(new CreateProcedureDto
        {
            PropertyId = property.Id,
            Type = ProcedureType.Other,
            TypeLabel = "Special Permit",
            Status = ProcedureStatus.Submitted,
            InternalNotes = "Confidential staff note",
            TotalFee = 50000m,
            AmountPaid = 10000m
        }, "staff");

        var summaries = await service.SearchProceduresAsync(new ProcedureSearchFilterDto());

        Assert.Single(summaries);

        // ProcedureSummaryDto must NOT have InternalNotes, TotalFee, AmountPaid, BalanceDue
        var summaryType = typeof(ProcedureSummaryDto);
        Assert.Null(summaryType.GetProperty("InternalNotes"));
        Assert.Null(summaryType.GetProperty("TotalFee"));
        Assert.Null(summaryType.GetProperty("AmountPaid"));
        Assert.Null(summaryType.GetProperty("BalanceDue"));
    }

    // ── Multiple Procedures & Domain Separation ──────────────────────────────

    [Fact]
    public async Task CreateMultipleProcedures_ForSingleProperty_ShouldAllPersistIndependently()
    {
        var (db, property) = await SeedPropertyAsync(nameof(CreateMultipleProcedures_ForSingleProperty_ShouldAllPersistIndependently));
        var service = new ProcedureService(db, NullLogger<ProcedureService>.Instance);

        var proc1 = await service.CreateProcedureAsync(new CreateProcedureDto
        {
            PropertyId = property.Id,
            Type = ProcedureType.Taqneen,
            ApplicationNumber = "TAQ-1001",
            ResponsibleAuthority = "State Land Authority",
            Status = ProcedureStatus.Submitted,
            TotalFee = 15000m,
            AmountPaid = 5000m,
            InternalNotes = "First stage inspection scheduled",
            MissingDocuments = "Survey report"
        }, "admin-1");

        var proc2 = await service.CreateProcedureAsync(new CreateProcedureDto
        {
            PropertyId = property.Id,
            Type = ProcedureType.Tasaleh,
            ApplicationNumber = "TAS-2002",
            ResponsibleAuthority = "Municipality",
            Status = ProcedureStatus.InProgress,
            TotalFee = 8000m,
            AmountPaid = 8000m,
            InternalNotes = "Structural assessment pending"
        }, "admin-1");

        var proc3 = await service.CreateProcedureAsync(new CreateProcedureDto
        {
            PropertyId = property.Id,
            Type = ProcedureType.UtilityConnection,
            ApplicationNumber = "ELEC-3003",
            ResponsibleAuthority = "National Electric Co",
            Status = ProcedureStatus.PendingRequirements,
            TotalFee = 3500m,
            AmountPaid = 0m,
            MissingDocuments = "Building permit copy"
        }, "admin-2");

        var proc4 = await service.CreateProcedureAsync(new CreateProcedureDto
        {
            PropertyId = property.Id,
            Type = ProcedureType.Other,
            TypeLabel = "Commercial Signboard Permit",
            ApplicationNumber = "SIGN-4004",
            ResponsibleAuthority = "City Beautification Department",
            Status = ProcedureStatus.Submitted
        }, "admin-2");

        Assert.True(proc1.Succeeded);
        Assert.True(proc2.Succeeded);
        Assert.True(proc3.Succeeded);
        Assert.True(proc4.Succeeded);

        var propertyProcedures = await service.GetProceduresByPropertyAsync(property.Id);
        Assert.Equal(4, propertyProcedures.Count);

        var detail1 = await service.GetProcedureByIdAsync(proc1.Data);
        var detail2 = await service.GetProcedureByIdAsync(proc2.Data);
        var detail3 = await service.GetProcedureByIdAsync(proc3.Data);
        var detail4 = await service.GetProcedureByIdAsync(proc4.Data);

        Assert.NotNull(detail1);
        Assert.NotNull(detail2);
        Assert.NotNull(detail3);
        Assert.NotNull(detail4);

        Assert.Equal(ProcedureType.Taqneen, detail1.Type);
        Assert.Equal(10000m, detail1.BalanceDue);
        Assert.Equal("TAQ-1001", detail1.ApplicationNumber);

        Assert.Equal(ProcedureType.Tasaleh, detail2.Type);
        Assert.Equal(0m, detail2.BalanceDue);
        Assert.Equal(ProcedureStatus.InProgress, detail2.Status);

        Assert.Equal(ProcedureType.UtilityConnection, detail3.Type);
        Assert.Equal(ProcedureStatus.PendingRequirements, detail3.Status);

        Assert.Equal(ProcedureType.Other, detail4.Type);
        Assert.Equal("Commercial Signboard Permit", detail4.TypeLabel);
    }

    // ── Sequential Status Transitions & Full Audit Trail ─────────────────────

    [Fact]
    public async Task ProcedureStatusLifecycle_SequentialTransitions_ShouldTrackFullAuditTrailWithTimestamps()
    {
        var (db, property) = await SeedPropertyAsync(nameof(ProcedureStatusLifecycle_SequentialTransitions_ShouldTrackFullAuditTrailWithTimestamps));
        var service = new ProcedureService(db, NullLogger<ProcedureService>.Instance);

        // 1. Initial creation (Submitted)
        var createResult = await service.CreateProcedureAsync(new CreateProcedureDto
        {
            PropertyId = property.Id,
            Type = ProcedureType.Taqneen,
            Status = ProcedureStatus.Submitted,
            ApplicationNumber = "LIFECYCLE-001"
        }, "staff-submitter");

        int procId = createResult.Data;
        var initial = await service.GetProcedureByIdAsync(procId);
        Assert.NotNull(initial);
        Assert.Equal(ProcedureStatus.Submitted, initial.Status);
        Assert.Empty(initial.StatusHistory);

        // 2. Transition: Submitted -> PendingRequirements
        var update1 = await service.UpdateProcedureAsync(new UpdateProcedureDto
        {
            Id = procId,
            PropertyId = property.Id,
            Type = ProcedureType.Taqneen,
            Status = ProcedureStatus.PendingRequirements,
            StatusChangeNote = "Authority requested deed copy and surveyor sketch.",
            MissingDocuments = "Deed copy, Surveyor sketch"
        }, "staff-case-worker");
        Assert.True(update1.Succeeded);

        // 3. Transition: PendingRequirements -> UnderReview
        var update2 = await service.UpdateProcedureAsync(new UpdateProcedureDto
        {
            Id = procId,
            PropertyId = property.Id,
            Type = ProcedureType.Taqneen,
            Status = ProcedureStatus.UnderReview,
            StatusChangeNote = "All requested documents submitted and accepted by authority committee.",
            MissingDocuments = null
        }, "staff-case-worker");
        Assert.True(update2.Succeeded);

        // 4. Transition: UnderReview -> Approved
        var update3 = await service.UpdateProcedureAsync(new UpdateProcedureDto
        {
            Id = procId,
            PropertyId = property.Id,
            Type = ProcedureType.Taqneen,
            Status = ProcedureStatus.Approved,
            StatusChangeNote = "Legalization committee issued preliminary approval."
        }, "staff-legal-lead");
        Assert.True(update3.Succeeded);

        // 5. Transition: Approved -> Completed (requires explicit authorized update)
        var update4 = await service.UpdateProcedureAsync(new UpdateProcedureDto
        {
            Id = procId,
            PropertyId = property.Id,
            Type = ProcedureType.Taqneen,
            Status = ProcedureStatus.Completed,
            StatusChangeNote = "Final legalization title registered. Procedure officially completed."
        }, "admin-manager");
        Assert.True(update4.Succeeded);

        // Verify full history
        var finalProcedure = await service.GetProcedureByIdAsync(procId);
        Assert.NotNull(finalProcedure);
        Assert.Equal(ProcedureStatus.Completed, finalProcedure.Status);
        Assert.Equal(4, finalProcedure.StatusHistory.Count);

        // Ordered by descending date in DTO
        var h0 = finalProcedure.StatusHistory[0]; // Most recent: Approved -> Completed
        Assert.Equal(ProcedureStatus.Approved, h0.FromStatus);
        Assert.Equal(ProcedureStatus.Completed, h0.ToStatus);
        Assert.Equal("admin-manager", h0.ChangedByUserId);
        Assert.Contains("Final legalization", h0.ChangeNote);
        Assert.True(h0.ChangedAtUtc <= DateTime.UtcNow);

        var h1 = finalProcedure.StatusHistory[1]; // UnderReview -> Approved
        Assert.Equal(ProcedureStatus.UnderReview, h1.FromStatus);
        Assert.Equal(ProcedureStatus.Approved, h1.ToStatus);
        Assert.Equal("staff-legal-lead", h1.ChangedByUserId);

        var h2 = finalProcedure.StatusHistory[2]; // PendingRequirements -> UnderReview
        Assert.Equal(ProcedureStatus.PendingRequirements, h2.FromStatus);
        Assert.Equal(ProcedureStatus.UnderReview, h2.ToStatus);

        var h3 = finalProcedure.StatusHistory[3]; // Submitted -> PendingRequirements
        Assert.Equal(ProcedureStatus.Submitted, h3.FromStatus);
        Assert.Equal(ProcedureStatus.PendingRequirements, h3.ToStatus);
        Assert.Equal("staff-case-worker", h3.ChangedByUserId);
    }

    // ── Non-Automatic Completion & Authorized Updates ─────────────────────────

    [Fact]
    public async Task ProcedureIsNotMarkedCompletedAutomatically_WithoutExplicitAuthorizedStatusUpdate()
    {
        var (db, property) = await SeedPropertyAsync(nameof(ProcedureIsNotMarkedCompletedAutomatically_WithoutExplicitAuthorizedStatusUpdate));
        var service = new ProcedureService(db, NullLogger<ProcedureService>.Instance);

        var create = await service.CreateProcedureAsync(new CreateProcedureDto
        {
            PropertyId = property.Id,
            Type = ProcedureType.Tasaleh,
            Status = ProcedureStatus.Submitted,
            TotalFee = 5000m,
            AmountPaid = 5000m // Paid in full
        }, "staff-user");

        var procId = create.Data;

        // Even when fully paid and updated with remarks, status must not magically become Completed
        await service.UpdateProcedureAsync(new UpdateProcedureDto
        {
            Id = procId,
            PropertyId = property.Id,
            Type = ProcedureType.Tasaleh,
            Status = ProcedureStatus.Submitted, // status remains Submitted
            TotalFee = 5000m,
            AmountPaid = 5000m,
            InternalNotes = "All fees paid in full."
        }, "staff-user");

        var current = await service.GetProcedureByIdAsync(procId);
        Assert.Equal(ProcedureStatus.Submitted, current!.Status);
        Assert.Empty(current.StatusHistory);
    }

    // ── Access Control & Controller Security ─────────────────────────────────

    [Fact]
    public void ProceduresController_RequiresAdminOrSuperAdminPolicy_AndGuardsPostActionsWithAntiForgeryToken()
    {
        // 1. Controller-level policy authorization check
        var authAttr = typeof(ProceduresController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal(AppPolicies.RequireAdminOrSuperAdmin, authAttr.Policy);

        // 2. ValidateAntiForgeryToken on Create POST
        var createPost = typeof(ProceduresController)
            .GetMethods()
            .FirstOrDefault(m => m.Name == "Create" && m.GetCustomAttribute<HttpPostAttribute>() != null);
        Assert.NotNull(createPost);
        Assert.NotNull(createPost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        // 3. ValidateAntiForgeryToken on Edit POST
        var editPost = typeof(ProceduresController)
            .GetMethods()
            .FirstOrDefault(m => m.Name == "Edit" && m.GetCustomAttribute<HttpPostAttribute>() != null);
        Assert.NotNull(editPost);
        Assert.NotNull(editPost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void CustomerIsolation_CustomerProfileDtoDoesNotContainProceduresOrStaffNotes()
    {
        var profileType = typeof(CustomerProfileDto);
        Assert.Null(profileType.GetProperty("Procedures"));
        Assert.Null(profileType.GetProperty("InternalNotes"));
        Assert.Null(profileType.GetProperty("TotalFee"));
        Assert.Null(profileType.GetProperty("BalanceDue"));
    }
}
