using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
using Xunit;

namespace Ardak.Tests;

public class DocumentManagementTests : IDisposable
{
    private readonly string _tempStorageDir;

    public DocumentManagementTests()
    {
        _tempStorageDir = Path.Combine(Path.GetTempPath(), "ArdakTests_SecureDocs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempStorageDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempStorageDir))
            {
                Directory.Delete(_tempStorageDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures on test temp directories
        }
    }

    // ── Test Fixture Helpers ─────────────────────────────────────────────────

    private FileDocumentStorageService CreateStorageService() =>
        new FileDocumentStorageService(_tempStorageDir, NullLogger<FileDocumentStorageService>.Instance);

    private static (IServiceProvider sp, ArdakDbContext db, UserManager<ApplicationUser> userManager) CreateContext(string dbName)
    {
        var sp = IdentityTestHelper.CreateTestServiceProvider(dbName);
        var db = sp.GetRequiredService<ArdakDbContext>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        return (sp, db, userManager);
    }

    private static async Task<ApplicationUser> CreateUserInRoleAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        string email,
        string role,
        int? customerId = null)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = $"{role} User",
            CustomerId = customerId
        };

        var result = await userManager.CreateAsync(user, "Password123!");
        Assert.True(result.Succeeded);
        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    private static async Task<(Customer customer, Property property, Procedure procedure)> SeedCustomerPropertyAndProcedureAsync(ArdakDbContext db)
    {
        var customer = new Customer
        {
            FullName = "Ahmed Al-Harbi",
            PhoneNumber = "0551122334",
            Email = "ahmed@example.com",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var property = new Property
        {
            ReferenceNumber = "PRP-DOC-001",
            Title = "Industrial Land Plot 44",
            Type = PropertyType.Land,
            Governorate = "Riyadh",
            City = "Riyadh",
            DistrictOrArea = "Al-Kharj Rd",
            AreaMeasurement = 1200,
            CustomerId = customer.Id
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var procedure = new Procedure
        {
            PropertyId = property.Id,
            Type = ProcedureType.Taqneen,
            ApplicationNumber = "TAQ-DOC-99",
            ResponsibleAuthority = "State Land Department",
            Status = ProcedureStatus.Submitted
        };
        db.Procedures.Add(procedure);
        await db.SaveChangesAsync();

        return (customer, property, procedure);
    }

    private static MemoryStream CreateValidPdfStream(string content = "Sample PDF content")
    {
        var stream = new MemoryStream();
        var header = Encoding.ASCII.GetBytes("%PDF-1.7\n");
        var body = Encoding.UTF8.GetBytes(content);
        stream.Write(header, 0, header.Length);
        stream.Write(body, 0, body.Length);
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateValidPngStream()
    {
        var stream = new MemoryStream();
        // PNG magic bytes: 0x89, 0x50, 0x4E, 0x47
        var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        stream.Write(pngHeader, 0, pngHeader.Length);
        stream.Position = 0;
        return stream;
    }

    // ── 1. File Validation Tests ─────────────────────────────────────────────

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".docx")]
    [InlineData(".xlsx")]
    [InlineData(".txt")]
    public void DocumentFileValidator_PermittedExtensions_AreAccepted(string ext)
    {
        var validator = new DocumentFileValidator();
        using var stream = ext switch
        {
            ".pdf" => CreateValidPdfStream(),
            ".png" => CreateValidPngStream(),
            ".jpg" => new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0 }),
            _ => new MemoryStream(Encoding.UTF8.GetBytes("Standard document test content"))
        };
        var result = validator.Validate(stream, $"testfile{ext}", "application/octet-stream", stream.Length);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal(ext, result.NormalizedExtension);
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".bat")]
    [InlineData(".cmd")]
    [InlineData(".sh")]
    [InlineData(".ps1")]
    [InlineData(".php")]
    [InlineData(".js")]
    public void DocumentFileValidator_BlacklistedExtensions_AreRejected(string ext)
    {
        var validator = new DocumentFileValidator();
        using var stream = new MemoryStream(new byte[100]);
        var result = validator.Validate(stream, $"malicious{ext}", "application/octet-stream", 100);

        Assert.False(result.IsValid);
        Assert.Contains("prohibited", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentFileValidator_UnrecognizedExtension_IsRejected()
    {
        var validator = new DocumentFileValidator();
        using var stream = new MemoryStream(new byte[100]);
        var result = validator.Validate(stream, "archive.tar", "application/octet-stream", 100);

        Assert.False(result.IsValid);
        Assert.Contains("not permitted", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentFileValidator_EmptyFile_IsRejected()
    {
        var validator = new DocumentFileValidator();
        using var stream = new MemoryStream();
        var result = validator.Validate(stream, "empty.pdf", "application/pdf", 0);

        Assert.False(result.IsValid);
        Assert.Contains("empty", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentFileValidator_OversizedFile_IsRejected()
    {
        var validator = new DocumentFileValidator(maxFileSizeBytes: 10 * 1024 * 1024); // 10MB limit
        using var stream = new MemoryStream();
        var result = validator.Validate(stream, "large.pdf", "application/pdf", 11 * 1024 * 1024);

        Assert.False(result.IsValid);
        Assert.Contains("exceeds the maximum", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../secret.pdf")]
    [InlineData("..\\secret.pdf")]
    [InlineData("folder/doc.pdf")]
    public void DocumentFileValidator_PathTraversalCharacters_AreRejected(string badName)
    {
        var validator = new DocumentFileValidator();
        using var stream = new MemoryStream(new byte[100]);
        var result = validator.Validate(stream, badName, "application/pdf", 100);

        Assert.False(result.IsValid);
        Assert.Contains("Invalid characters", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentFileValidator_InvalidPdfHeader_IsRejected()
    {
        var validator = new DocumentFileValidator();
        using var fakePdfStream = new MemoryStream(Encoding.UTF8.GetBytes("Not a real PDF header"));
        var result = validator.Validate(fakePdfStream, "spoofed.pdf", "application/pdf", fakePdfStream.Length);

        Assert.False(result.IsValid);
        Assert.Contains("does not match standard PDF", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── 2. Secure Upload & Storage Tests ─────────────────────────────────────

    [Fact]
    public async Task UploadDocument_ValidInput_DefaultsToPrivateAndGeneratesOpaqueKey()
    {
        var (sp, db, userManager) = CreateContext(nameof(UploadDocument_ValidInput_DefaultsToPrivateAndGeneratesOpaqueKey));
        var storage = CreateStorageService();
        var validator = new DocumentFileValidator();
        var service = new DocumentService(db, storage, validator, userManager, NullLogger<DocumentService>.Instance);

        var (customer, property, _) = await SeedCustomerPropertyAndProcedureAsync(db);

        using var pdf = CreateValidPdfStream("Legal title survey notes");
        var dto = new UploadDocumentDto
        {
            PropertyId = property.Id,
            Type = DocumentType.DeedOrTitle,
            Title = "Official Title Deed",
            ReferenceNumber = "DEED-9988",
            ConfidentialityLevel = DocumentConfidentialityLevel.Confidential,
            InternalNotes = "Verified by notary public",
            IsCustomerVisible = false // Private by default
        };

        var result = await service.UploadDocumentAsync(dto, pdf, "Deed_Scan.pdf", "application/pdf", pdf.Length, "admin-1");

        Assert.True(result.Succeeded);
        Assert.True(result.Data > 0);

        var saved = await db.Documents.FindAsync(result.Data);
        Assert.NotNull(saved);
        Assert.False(saved.IsCustomerVisible, "Document MUST be private by default.");
        Assert.False(saved.IsArchived);
        Assert.Equal("Deed_Scan.pdf", saved.OriginalFileName);
        Assert.EndsWith(".bin", saved.StorageKey); // Opaque GUID token
        Assert.DoesNotContain("/", saved.StorageKey);
        Assert.DoesNotContain("\\", saved.StorageKey);
        Assert.NotNull(saved.ChecksumSha256);
        Assert.Equal(64, saved.ChecksumSha256.Length); // SHA-256 hex string

        // Verify physical file was written to private storage
        var fileExists = await storage.FileExistsAsync(saved.StorageKey);
        Assert.True(fileExists);
    }

    [Fact]
    public async Task UploadDocument_NonexistentEntity_ShouldFail()
    {
        var (sp, db, userManager) = CreateContext(nameof(UploadDocument_NonexistentEntity_ShouldFail));
        var storage = CreateStorageService();
        var validator = new DocumentFileValidator();
        var service = new DocumentService(db, storage, validator, userManager, NullLogger<DocumentService>.Instance);

        using var pdf = CreateValidPdfStream();
        var dto = new UploadDocumentDto
        {
            PropertyId = 99999, // Nonexistent
            Title = "Orphan Document",
            Type = DocumentType.Other
        };

        var result = await service.UploadDocumentAsync(dto, pdf, "orphan.pdf", "application/pdf", pdf.Length, "admin-1");

        Assert.False(result.Succeeded);
        Assert.Contains("property does not exist", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── 3. Archival & Historical Record Preservation ─────────────────────────

    [Fact]
    public async Task ArchiveDocument_MarksArchived_PreservesRecordAndStorageFile()
    {
        var (sp, db, userManager) = CreateContext(nameof(ArchiveDocument_MarksArchived_PreservesRecordAndStorageFile));
        var storage = CreateStorageService();
        var validator = new DocumentFileValidator();
        var service = new DocumentService(db, storage, validator, userManager, NullLogger<DocumentService>.Instance);

        var (customer, property, _) = await SeedCustomerPropertyAndProcedureAsync(db);

        using var pdf = CreateValidPdfStream("Historic boundary record");
        var upload = await service.UploadDocumentAsync(new UploadDocumentDto
        {
            PropertyId = property.Id,
            Title = "Historical Boundary Demarcation",
            Type = DocumentType.SurveyReport
        }, pdf, "boundary.pdf", "application/pdf", pdf.Length, "admin-1");

        int docId = upload.Data;

        // Archive document
        var archiveResult = await service.ArchiveDocumentAsync(docId, "compliance-officer");
        Assert.True(archiveResult.Succeeded);

        var archivedDoc = await db.Documents.FindAsync(docId);
        Assert.NotNull(archivedDoc);
        Assert.True(archivedDoc.IsArchived);
        Assert.NotNull(archivedDoc.ArchivedAtUtc);
        Assert.Equal("compliance-officer", archivedDoc.ArchivedByUserId);

        // Crucial requirement: Historical files must NOT be deleted from storage
        var fileStillInStorage = await storage.FileExistsAsync(archivedDoc.StorageKey);
        Assert.True(fileStillInStorage, "Archived documents must remain preserved in storage for historical compliance.");
    }

    // ── 4. Server-Side Authorization & Download Permissions ───────────────────

    [Fact]
    public async Task StaffMembers_CanDownloadAnyDocument()
    {
        var (sp, db, userManager) = CreateContext(nameof(StaffMembers_CanDownloadAnyDocument));
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var storage = CreateStorageService();
        var validator = new DocumentFileValidator();
        var service = new DocumentService(db, storage, validator, userManager, NullLogger<DocumentService>.Instance);

        var adminUser = await CreateUserInRoleAsync(userManager, roleManager, "staff@ardak.internal", AppRoles.Admin);
        var (customer, property, _) = await SeedCustomerPropertyAndProcedureAsync(db);

        using var pdf = CreateValidPdfStream("Secret administrative memo");
        var upload = await service.UploadDocumentAsync(new UploadDocumentDto
        {
            PropertyId = property.Id,
            Title = "Internal Property Memo",
            Type = DocumentType.Other,
            ConfidentialityLevel = DocumentConfidentialityLevel.StrictlyConfidential,
            IsCustomerVisible = false // Strictly private
        }, pdf, "memo.pdf", "application/pdf", pdf.Length, adminUser.Id);

        // Admin download request
        var downloadResult = await service.GetDocumentDownloadAsync(upload.Data, adminUser.Id);

        Assert.True(downloadResult.Succeeded);
        using var resultStream = downloadResult.Data!.Stream;
        Assert.NotNull(resultStream);
        Assert.Equal("memo.pdf", downloadResult.Data.DownloadFileName);
        Assert.Equal("application/pdf", downloadResult.Data.ContentType);
    }

    [Fact]
    public async Task Customer_CannotDownloadPrivateDocument_EvenIfAssociatedWithTheirProperty()
    {
        var (sp, db, userManager) = CreateContext(nameof(Customer_CannotDownloadPrivateDocument_EvenIfAssociatedWithTheirProperty));
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var storage = CreateStorageService();
        var validator = new DocumentFileValidator();
        var service = new DocumentService(db, storage, validator, userManager, NullLogger<DocumentService>.Instance);

        var (customer, property, _) = await SeedCustomerPropertyAndProcedureAsync(db);
        var customerUser = await CreateUserInRoleAsync(userManager, roleManager, "client@domain.com", AppRoles.Customer, customer.Id);

        using var pdf = CreateValidPdfStream("Internal appraisal confidential");
        var upload = await service.UploadDocumentAsync(new UploadDocumentDto
        {
            PropertyId = property.Id,
            CustomerId = customer.Id,
            Title = "Internal Financial Assessment",
            Type = DocumentType.Other,
            IsCustomerVisible = false // Private (not approved for customer visibility)
        }, pdf, "appraisal.pdf", "application/pdf", pdf.Length, "admin-1");

        // Attempt download by property owner customer
        var downloadResult = await service.GetDocumentDownloadAsync(upload.Data, customerUser.Id);

        Assert.False(downloadResult.Succeeded, "Customer MUST be prohibited from downloading private documents.");
        Assert.Contains("Unauthorized", downloadResult.Error);
    }

    [Fact]
    public async Task Customer_CannotDownloadDocument_BelongingToDifferentCustomer()
    {
        var (sp, db, userManager) = CreateContext(nameof(Customer_CannotDownloadDocument_BelongingToDifferentCustomer));
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var storage = CreateStorageService();
        var validator = new DocumentFileValidator();
        var service = new DocumentService(db, storage, validator, userManager, NullLogger<DocumentService>.Instance);

        var (legitCustomer, property, _) = await SeedCustomerPropertyAndProcedureAsync(db);

        // Different, unauthorized customer
        var otherCustomer = new Customer { FullName = "Other Person", PhoneNumber = "0599999999", Status = CustomerStatus.Active };
        db.Customers.Add(otherCustomer);
        await db.SaveChangesAsync();

        var attackerCustomerUser = await CreateUserInRoleAsync(userManager, roleManager, "attacker@domain.com", AppRoles.Customer, otherCustomer.Id);

        using var pdf = CreateValidPdfStream("Public deed for legitimate customer");
        var upload = await service.UploadDocumentAsync(new UploadDocumentDto
        {
            PropertyId = property.Id,
            CustomerId = legitCustomer.Id,
            Title = "Deed Approved for Customer",
            Type = DocumentType.DeedOrTitle,
            IsCustomerVisible = true // Visible, BUT only to legitCustomer
        }, pdf, "deed.pdf", "application/pdf", pdf.Length, "admin-1");

        // Attacker attempts to download legitCustomer's document
        var downloadResult = await service.GetDocumentDownloadAsync(upload.Data, attackerCustomerUser.Id);

        Assert.False(downloadResult.Succeeded, "Cross-customer document access MUST be denied.");
        Assert.Contains("Unauthorized", downloadResult.Error);
    }

    [Fact]
    public async Task Customer_CanDownloadApprovedDocument_AssociatedWithTheirProperty()
    {
        var (sp, db, userManager) = CreateContext(nameof(Customer_CanDownloadApprovedDocument_AssociatedWithTheirProperty));
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var storage = CreateStorageService();
        var validator = new DocumentFileValidator();
        var service = new DocumentService(db, storage, validator, userManager, NullLogger<DocumentService>.Instance);

        var (customer, property, _) = await SeedCustomerPropertyAndProcedureAsync(db);
        var customerUser = await CreateUserInRoleAsync(userManager, roleManager, "ahmed@domain.com", AppRoles.Customer, customer.Id);

        using var pdf = CreateValidPdfStream("Approved building permit");
        var upload = await service.UploadDocumentAsync(new UploadDocumentDto
        {
            PropertyId = property.Id,
            CustomerId = customer.Id,
            Title = "Approved Building Permit",
            Type = DocumentType.UtilityPermit,
            IsCustomerVisible = true // Explicitly approved for customer visibility
        }, pdf, "permit.pdf", "application/pdf", pdf.Length, "admin-1");

        // Customer attempts authorized download
        var downloadResult = await service.GetDocumentDownloadAsync(upload.Data, customerUser.Id);

        Assert.True(downloadResult.Succeeded, "Customer should be able to download an explicitly approved document for their property.");
        using var stream = downloadResult.Data!.Stream;
        Assert.NotNull(stream);
        Assert.Equal("permit.pdf", downloadResult.Data.DownloadFileName);
    }

    [Fact]
    public async Task Customer_CannotDownloadArchivedDocument_EvenIfCustomerVisibleWasTrue()
    {
        var (sp, db, userManager) = CreateContext(nameof(Customer_CannotDownloadArchivedDocument_EvenIfCustomerVisibleWasTrue));
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var storage = CreateStorageService();
        var validator = new DocumentFileValidator();
        var service = new DocumentService(db, storage, validator, userManager, NullLogger<DocumentService>.Instance);

        var (customer, property, _) = await SeedCustomerPropertyAndProcedureAsync(db);
        var customerUser = await CreateUserInRoleAsync(userManager, roleManager, "ahmed_archived@domain.com", AppRoles.Customer, customer.Id);

        using var pdf = CreateValidPdfStream("Old superseded deed");
        var upload = await service.UploadDocumentAsync(new UploadDocumentDto
        {
            PropertyId = property.Id,
            CustomerId = customer.Id,
            Title = "Superseded Deed",
            Type = DocumentType.DeedOrTitle,
            IsCustomerVisible = true // Visible when active
        }, pdf, "old_deed.pdf", "application/pdf", pdf.Length, "admin-1");

        // Archive the document
        await service.ArchiveDocumentAsync(upload.Data, "admin-1");

        // Customer attempts to download archived document
        var downloadResult = await service.GetDocumentDownloadAsync(upload.Data, customerUser.Id);

        Assert.False(downloadResult.Succeeded, "Customers must never be permitted to access archived records.");
    }

    // ── 5. Confidentiality & Data Masking Tests ──────────────────────────────

    [Fact]
    public async Task CustomerDetailDto_NeverExposesInternalNotesOrStorageCredentials()
    {
        var (sp, db, userManager) = CreateContext(nameof(CustomerDetailDto_NeverExposesInternalNotesOrStorageCredentials));
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var storage = CreateStorageService();
        var validator = new DocumentFileValidator();
        var service = new DocumentService(db, storage, validator, userManager, NullLogger<DocumentService>.Instance);

        var (customer, property, _) = await SeedCustomerPropertyAndProcedureAsync(db);
        var customerUser = await CreateUserInRoleAsync(userManager, roleManager, "customer_notes@domain.com", AppRoles.Customer, customer.Id);

        using var pdf = CreateValidPdfStream("Document with confidential remarks");
        var upload = await service.UploadDocumentAsync(new UploadDocumentDto
        {
            PropertyId = property.Id,
            CustomerId = customer.Id,
            Title = "Approved Site Plan",
            Type = DocumentType.EngineeringPlan,
            InternalNotes = "Private financial remark for staff only",
            IsCustomerVisible = true
        }, pdf, "plan.pdf", "application/pdf", pdf.Length, "admin-1");

        // Detail retrieved by customer
        var detail = await service.GetDocumentByIdAsync(upload.Data, customerUser.Id);

        Assert.NotNull(detail);
        Assert.Null(detail.InternalNotes); // Masked for customer
        Assert.Null(detail.ArchivedByUserId);
        Assert.Null(detail.UploadedByUserId);

        // Verify summary DTO does not even have InternalNotes property
        var summaryProps = typeof(DocumentSummaryDto).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("InternalNotes", summaryProps);
        Assert.DoesNotContain("StorageKey", summaryProps);
    }

    // ── 6. Controller Security & Antiforgery Tests ────────────────────────────

    [Fact]
    public void DocumentsController_EnforcesSuperAdminOrAdminPolicy_AndCSRFProtection()
    {
        var controllerType = typeof(DocumentsController);

        // 1. Controller-level policy check
        var authAttr = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal(AppPolicies.RequireAdminOrSuperAdmin, authAttr.Policy);

        // 2. ValidateAntiForgeryToken on Upload POST
        var uploadPost = controllerType.GetMethods()
            .FirstOrDefault(m => m.Name == "Upload" && m.GetCustomAttribute<HttpPostAttribute>() != null);
        Assert.NotNull(uploadPost);
        Assert.NotNull(uploadPost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        // 3. ValidateAntiForgeryToken on Edit POST
        var editPost = controllerType.GetMethods()
            .FirstOrDefault(m => m.Name == "Edit" && m.GetCustomAttribute<HttpPostAttribute>() != null);
        Assert.NotNull(editPost);
        Assert.NotNull(editPost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        // 4. ValidateAntiForgeryToken on Archive POST
        var archivePost = controllerType.GetMethods()
            .FirstOrDefault(m => m.Name == "Archive" && m.GetCustomAttribute<HttpPostAttribute>() != null);
        Assert.NotNull(archivePost);
        Assert.NotNull(archivePost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }
}
