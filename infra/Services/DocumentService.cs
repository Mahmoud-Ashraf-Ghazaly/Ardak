using System.Security.Cryptography;
using Ardak.Application.DTOs;
using Ardak.Application.Interfaces;
using Ardak.Infrastructure.Data;
using Ardak.Infrastructure.Identity;
using Ardak.Model.Constants;
using Ardak.Model.Entities;
using Ardak.Model.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ardak.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private readonly ArdakDbContext _db;
    private readonly IDocumentStorageService _storageService;
    private readonly IDocumentFileValidator _fileValidator;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        ArdakDbContext db,
        IDocumentStorageService storageService,
        IDocumentFileValidator fileValidator,
        UserManager<ApplicationUser> userManager,
        ILogger<DocumentService> logger)
    {
        _db = db;
        _storageService = storageService;
        _fileValidator = fileValidator;
        _userManager = userManager;
        _logger = logger;
    }

    // ── Upload ───────────────────────────────────────────────────────────────

    public async Task<ServiceResult<int>> UploadDocumentAsync(
        UploadDocumentDto dto,
        Stream fileStream,
        string fileName,
        string? contentType,
        long fileLength,
        string currentUserId)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (fileStream == null) return ServiceResult<int>.Failure("No file stream provided.");

        // 1. File Validation
        var validation = _fileValidator.Validate(fileStream, fileName, contentType, fileLength);
        if (!validation.IsValid)
        {
            return ServiceResult<int>.Failure(validation.ErrorMessage ?? "Invalid file.");
        }

        // 2. Validate Associations if provided
        if (dto.CustomerId.HasValue)
        {
            var customerExists = await _db.Customers.AnyAsync(c => c.Id == dto.CustomerId.Value);
            if (!customerExists)
                return ServiceResult<int>.Failure("The specified customer does not exist.");
        }

        if (dto.PropertyId.HasValue)
        {
            var propertyExists = await _db.Properties.AnyAsync(p => p.Id == dto.PropertyId.Value);
            if (!propertyExists)
                return ServiceResult<int>.Failure("The specified property does not exist.");
        }

        if (dto.ProcedureId.HasValue)
        {
            var procedureExists = await _db.Procedures.AnyAsync(pr => pr.Id == dto.ProcedureId.Value);
            if (!procedureExists)
                return ServiceResult<int>.Failure("The specified procedure does not exist.");
        }

        // 3. Compute Checksum (SHA-256)
        string checksumSha256;
        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
            using (var sha = SHA256.Create())
            {
                var hash = await Task.Run(() => sha.ComputeHash(fileStream));
                checksumSha256 = Convert.ToHexString(hash).ToLowerInvariant();
            }
            fileStream.Position = 0;
        }
        else
        {
            checksumSha256 = string.Empty;
        }

        // 4. Save to secure storage (isolated from wwwroot)
        var resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        var storageKey = await _storageService.SaveFileAsync(fileStream, validation.SanitizedFileName, resolvedContentType);

        // 5. Persist Document Record
        var document = new Document
        {
            CustomerId = dto.CustomerId,
            PropertyId = dto.PropertyId,
            ProcedureId = dto.ProcedureId,
            Type = dto.Type,
            TypeLabel = string.IsNullOrWhiteSpace(dto.TypeLabel) ? null : dto.TypeLabel.Trim(),
            Title = dto.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            ReferenceNumber = string.IsNullOrWhiteSpace(dto.ReferenceNumber) ? null : dto.ReferenceNumber.Trim(),
            OriginalFileName = validation.SanitizedFileName,
            StorageKey = storageKey,
            ContentType = resolvedContentType,
            FileSizeBytes = fileLength,
            FileExtension = validation.NormalizedExtension,
            ChecksumSha256 = checksumSha256,
            ConfidentialityLevel = dto.ConfidentialityLevel,
            IsCustomerVisible = dto.IsCustomerVisible, // Private by default unless explicitly checked
            InternalNotes = string.IsNullOrWhiteSpace(dto.InternalNotes) ? null : dto.InternalNotes.Trim(),
            IsArchived = false,
            UploadedAtUtc = DateTime.UtcNow,
            UploadedByUserId = currentUserId
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Staff '{UserId}' uploaded document ID {DocumentId} ('{Title}') [Type: {Type}, VisibleToCustomer: {Visible}].",
            currentUserId, document.Id, document.Title, document.Type, document.IsCustomerVisible);

        return ServiceResult<int>.Success(document.Id);
    }

    // ── Get Detail ───────────────────────────────────────────────────────────

    public async Task<DocumentDetailDto?> GetDocumentByIdAsync(int id, string currentUserId)
    {
        var hasAccess = await CanUserAccessDocumentAsync(id, currentUserId, isDownload: false);
        if (!hasAccess)
        {
            return null;
        }

        var doc = await _db.Documents
            .AsNoTracking()
            .Include(d => d.Customer)
            .Include(d => d.Property)
            .Include(d => d.Procedure)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null) return null;

        var user = await _userManager.FindByIdAsync(currentUserId);
        var isCustomer = user != null && await _userManager.IsInRoleAsync(user, AppRoles.Customer);

        return new DocumentDetailDto
        {
            Id = doc.Id,
            CustomerId = doc.CustomerId,
            CustomerName = doc.Customer?.FullName,
            PropertyId = doc.PropertyId,
            PropertyReferenceNumber = doc.Property?.ReferenceNumber,
            PropertyTitle = doc.Property?.Title,
            ProcedureId = doc.ProcedureId,
            ProcedureTypeLabel = doc.Procedure?.TypeLabel ?? doc.Procedure?.Type.ToString(),
            ProcedureApplicationNumber = doc.Procedure?.ApplicationNumber,
            Type = doc.Type,
            TypeLabel = doc.TypeLabel ?? doc.Type.ToString(),
            Title = doc.Title,
            Description = doc.Description,
            ReferenceNumber = doc.ReferenceNumber,
            OriginalFileName = doc.OriginalFileName,
            ContentType = doc.ContentType,
            FileSizeBytes = doc.FileSizeBytes,
            FileExtension = doc.FileExtension,
            ChecksumSha256 = doc.ChecksumSha256,
            ConfidentialityLevel = doc.ConfidentialityLevel,
            IsCustomerVisible = doc.IsCustomerVisible,
            // Mask internal notes if accessed by customer
            InternalNotes = isCustomer ? null : doc.InternalNotes,
            IsArchived = doc.IsArchived,
            ArchivedAtUtc = doc.ArchivedAtUtc,
            ArchivedByUserId = isCustomer ? null : doc.ArchivedByUserId,
            UploadedAtUtc = doc.UploadedAtUtc,
            UploadedByUserId = isCustomer ? null : doc.UploadedByUserId,
            ModifiedAtUtc = doc.ModifiedAtUtc,
            ModifiedByUserId = isCustomer ? null : doc.ModifiedByUserId
        };
    }

    // ── Search & Listings ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DocumentSummaryDto>> SearchDocumentsAsync(DocumentSearchFilterDto filter, string currentUserId)
    {
        var user = await _userManager.FindByIdAsync(currentUserId);
        if (user == null) return Array.Empty<DocumentSummaryDto>();

        var isSuperAdmin = await _userManager.IsInRoleAsync(user, AppRoles.SuperAdmin);
        var isAdmin = await _userManager.IsInRoleAsync(user, AppRoles.Admin);
        var isCustomer = await _userManager.IsInRoleAsync(user, AppRoles.Customer);

        if (!isSuperAdmin && !isAdmin && !isCustomer)
        {
            return Array.Empty<DocumentSummaryDto>();
        }

        var query = _db.Documents
            .AsNoTracking()
            .Include(d => d.Customer)
            .Include(d => d.Property)
                .ThenInclude(p => p!.CustomerAssociations)
            .Include(d => d.Procedure)
                .ThenInclude(pr => pr!.Property)
                    .ThenInclude(prp => prp!.CustomerAssociations)
            .AsQueryable();

        // Customer Isolation Enforcement
        if (isCustomer)
        {
            if (!user.CustomerId.HasValue) return Array.Empty<DocumentSummaryDto>();
            var custId = user.CustomerId.Value;

            query = query.Where(d =>
                d.IsCustomerVisible &&
                !d.IsArchived &&
                (
                    (d.CustomerId.HasValue && d.CustomerId.Value == custId) ||
                    (d.Property != null && (d.Property.CustomerId == custId || d.Property.CustomerAssociations.Any(ca => ca.CustomerId == custId))) ||
                    (d.Procedure != null && d.Procedure.Property != null && (d.Procedure.Property.CustomerId == custId || d.Procedure.Property.CustomerAssociations.Any(ca => ca.CustomerId == custId)))
                )
            );
        }

        // Apply Search Filters
        if (filter.CustomerId.HasValue)
            query = query.Where(d => d.CustomerId == filter.CustomerId.Value);

        if (filter.PropertyId.HasValue)
            query = query.Where(d => d.PropertyId == filter.PropertyId.Value);

        if (filter.ProcedureId.HasValue)
            query = query.Where(d => d.ProcedureId == filter.ProcedureId.Value);

        if (filter.Type.HasValue)
            query = query.Where(d => d.Type == filter.Type.Value);

        if (filter.ConfidentialityLevel.HasValue)
            query = query.Where(d => d.ConfidentialityLevel == filter.ConfidentialityLevel.Value);

        if (filter.IsCustomerVisible.HasValue)
            query = query.Where(d => d.IsCustomerVisible == filter.IsCustomerVisible.Value);

        if (filter.IsArchived.HasValue)
            query = query.Where(d => d.IsArchived == filter.IsArchived.Value);
        else if (isCustomer)
            query = query.Where(d => !d.IsArchived);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(d =>
                d.Title.Contains(term) ||
                (d.ReferenceNumber != null && d.ReferenceNumber.Contains(term)) ||
                d.OriginalFileName.Contains(term));
        }

        return await query
            .OrderByDescending(d => d.UploadedAtUtc)
            .Select(d => new DocumentSummaryDto
            {
                Id = d.Id,
                CustomerId = d.CustomerId,
                CustomerName = d.Customer != null ? d.Customer.FullName : null,
                PropertyId = d.PropertyId,
                PropertyReferenceNumber = d.Property != null ? d.Property.ReferenceNumber : null,
                PropertyTitle = d.Property != null ? d.Property.Title : null,
                ProcedureId = d.ProcedureId,
                ProcedureTypeLabel = d.Procedure != null ? (d.Procedure.TypeLabel ?? d.Procedure.Type.ToString()) : null,
                ProcedureApplicationNumber = d.Procedure != null ? d.Procedure.ApplicationNumber : null,
                Type = d.Type,
                TypeLabel = d.TypeLabel ?? d.Type.ToString(),
                Title = d.Title,
                ReferenceNumber = d.ReferenceNumber,
                OriginalFileName = d.OriginalFileName,
                ContentType = d.ContentType,
                FileSizeBytes = d.FileSizeBytes,
                FileExtension = d.FileExtension,
                ConfidentialityLevel = d.ConfidentialityLevel,
                IsCustomerVisible = d.IsCustomerVisible,
                IsArchived = d.IsArchived,
                UploadedAtUtc = d.UploadedAtUtc,
                UploadedByUserId = d.UploadedByUserId
            })
            .ToListAsync();
    }

    public Task<IReadOnlyList<DocumentSummaryDto>> GetDocumentsByCustomerAsync(int customerId, string currentUserId)
        => SearchDocumentsAsync(new DocumentSearchFilterDto { CustomerId = customerId }, currentUserId);

    public Task<IReadOnlyList<DocumentSummaryDto>> GetDocumentsByPropertyAsync(int propertyId, string currentUserId)
        => SearchDocumentsAsync(new DocumentSearchFilterDto { PropertyId = propertyId }, currentUserId);

    public Task<IReadOnlyList<DocumentSummaryDto>> GetDocumentsByProcedureAsync(int procedureId, string currentUserId)
        => SearchDocumentsAsync(new DocumentSearchFilterDto { ProcedureId = procedureId }, currentUserId);

    // ── Updates & Visibility ─────────────────────────────────────────────────

    public async Task<ServiceResult> UpdateDocumentMetadataAsync(UpdateDocumentMetadataDto dto, string currentUserId)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        var doc = await _db.Documents.FindAsync(dto.Id);
        if (doc == null)
            return ServiceResult.Failure("Document record not found.");

        doc.Title = dto.Title.Trim();
        doc.Type = dto.Type;
        doc.TypeLabel = string.IsNullOrWhiteSpace(dto.TypeLabel) ? null : dto.TypeLabel.Trim();
        doc.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        doc.ReferenceNumber = string.IsNullOrWhiteSpace(dto.ReferenceNumber) ? null : dto.ReferenceNumber.Trim();
        doc.ConfidentialityLevel = dto.ConfidentialityLevel;
        doc.IsCustomerVisible = dto.IsCustomerVisible;
        doc.InternalNotes = string.IsNullOrWhiteSpace(dto.InternalNotes) ? null : dto.InternalNotes.Trim();
        doc.ModifiedAtUtc = DateTime.UtcNow;
        doc.ModifiedByUserId = currentUserId;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Staff '{UserId}' updated document ID {DocumentId} metadata.", currentUserId, doc.Id);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> SetCustomerVisibilityAsync(int documentId, bool isVisible, string currentUserId)
    {
        var doc = await _db.Documents.FindAsync(documentId);
        if (doc == null)
            return ServiceResult.Failure("Document record not found.");

        doc.IsCustomerVisible = isVisible;
        doc.ModifiedAtUtc = DateTime.UtcNow;
        doc.ModifiedByUserId = currentUserId;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Staff '{UserId}' set document ID {DocumentId} customer visibility to {IsVisible}.",
            currentUserId, documentId, isVisible);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ArchiveDocumentAsync(int documentId, string currentUserId)
    {
        var doc = await _db.Documents.FindAsync(documentId);
        if (doc == null)
            return ServiceResult.Failure("Document record not found.");

        doc.IsArchived = true;
        doc.ArchivedAtUtc = DateTime.UtcNow;
        doc.ArchivedByUserId = currentUserId;
        doc.ModifiedAtUtc = DateTime.UtcNow;
        doc.ModifiedByUserId = currentUserId;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Staff '{UserId}' archived document ID {DocumentId}.", currentUserId, documentId);
        return ServiceResult.Success();
    }

    // ── Secure Download with Server-Side Authorization ───────────────────────

    public async Task<ServiceResult<DocumentDownloadResult>> GetDocumentDownloadAsync(int documentId, string currentUserId)
    {
        var isAuthorized = await CanUserAccessDocumentAsync(documentId, currentUserId, isDownload: true);
        if (!isAuthorized)
        {
            _logger.LogWarning("Access violation: User '{UserId}' attempted to download unauthorized document ID {DocumentId}.",
                currentUserId, documentId);
            return ServiceResult<DocumentDownloadResult>.Failure("Unauthorized to access or download this document.");
        }

        var doc = await _db.Documents.FindAsync(documentId);
        if (doc == null)
        {
            return ServiceResult<DocumentDownloadResult>.Failure("Document record not found.");
        }

        var stream = await _storageService.GetFileStreamAsync(doc.StorageKey);
        if (stream == null)
        {
            _logger.LogError("File content missing from storage for document ID {DocumentId} (StorageKey: {Key}).",
                doc.Id, doc.StorageKey);
            return ServiceResult<DocumentDownloadResult>.Failure("File content could not be located in secure storage.");
        }

        var downloadResult = new DocumentDownloadResult
        {
            Stream = stream,
            ContentType = doc.ContentType,
            DownloadFileName = doc.OriginalFileName,
            FileSizeBytes = doc.FileSizeBytes
        };

        return ServiceResult<DocumentDownloadResult>.Success(downloadResult);
    }

    // ── Authorization Validation ─────────────────────────────────────────────

    public async Task<bool> CanUserAccessDocumentAsync(int documentId, string currentUserId, bool isDownload)
    {
        if (string.IsNullOrWhiteSpace(currentUserId)) return false;

        var user = await _userManager.FindByIdAsync(currentUserId);
        if (user == null) return false;

        var isSuperAdmin = await _userManager.IsInRoleAsync(user, AppRoles.SuperAdmin);
        var isAdmin = await _userManager.IsInRoleAsync(user, AppRoles.Admin);

        // Admins and SuperAdmins have access to view and download all documents (including archived)
        if (isSuperAdmin || isAdmin) return true;

        var isCustomer = await _userManager.IsInRoleAsync(user, AppRoles.Customer);
        if (!isCustomer || !user.CustomerId.HasValue) return false;

        // Customer Access Verification
        var doc = await _db.Documents
            .AsNoTracking()
            .Include(d => d.Property)
                .ThenInclude(p => p!.CustomerAssociations)
            .Include(d => d.Procedure)
                .ThenInclude(pr => pr!.Property)
                    .ThenInclude(prp => prp!.CustomerAssociations)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (doc == null) return false;

        // Customers can NEVER access archived documents
        if (doc.IsArchived) return false;

        // Customers can ONLY access documents explicitly approved for customer access
        if (!doc.IsCustomerVisible) return false;

        var custId = user.CustomerId.Value;

        // Verify customer owns or is associated with the entity linked to the document:
        if (doc.CustomerId.HasValue && doc.CustomerId.Value == custId)
        {
            return true;
        }

        if (doc.Property != null)
        {
            if (doc.Property.CustomerId == custId) return true;
            if (doc.Property.CustomerAssociations.Any(ca => ca.CustomerId == custId)) return true;
        }

        if (doc.Procedure?.Property != null)
        {
            if (doc.Procedure.Property.CustomerId == custId) return true;
            if (doc.Procedure.Property.CustomerAssociations.Any(ca => ca.CustomerId == custId)) return true;
        }

        return false;
    }
}
