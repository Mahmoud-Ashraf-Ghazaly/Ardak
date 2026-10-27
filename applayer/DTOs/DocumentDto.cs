using Ardak.Model.Enums;

namespace Ardak.Application.DTOs;

/// <summary>
/// Summary view of a document for listings and overview grids.
/// Internal notes, physical storage keys, and credentials are intentionally excluded.
/// </summary>
public class DocumentSummaryDto
{
    public int Id { get; set; }

    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }

    public int? PropertyId { get; set; }
    public string? PropertyReferenceNumber { get; set; }
    public string? PropertyTitle { get; set; }

    public int? ProcedureId { get; set; }
    public string? ProcedureTypeLabel { get; set; }
    public string? ProcedureApplicationNumber { get; set; }

    public DocumentType Type { get; set; }
    public string TypeLabel { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileExtension { get; set; } = string.Empty;

    public DocumentConfidentialityLevel ConfidentialityLevel { get; set; }
    public bool IsCustomerVisible { get; set; }
    public bool IsArchived { get; set; }

    public DateTime UploadedAtUtc { get; set; }
    public string? UploadedByUserId { get; set; }

    public string FormattedFileSize => FormatFileSize(FileSizeBytes);

    public static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}

/// <summary>
/// Full internal detail view for authorized staff.
/// Includes confidential fields such as InternalNotes and ChecksumSha256.
/// Physical storage paths and storage credentials are never included.
/// </summary>
public class DocumentDetailDto
{
    public int Id { get; set; }

    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }

    public int? PropertyId { get; set; }
    public string? PropertyReferenceNumber { get; set; }
    public string? PropertyTitle { get; set; }

    public int? ProcedureId { get; set; }
    public string? ProcedureTypeLabel { get; set; }
    public string? ProcedureApplicationNumber { get; set; }

    public DocumentType Type { get; set; }
    public string TypeLabel { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ReferenceNumber { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileExtension { get; set; } = string.Empty;
    public string? ChecksumSha256 { get; set; }

    public DocumentConfidentialityLevel ConfidentialityLevel { get; set; }
    public bool IsCustomerVisible { get; set; }

    /// <summary>Confidential staff-only internal notes.</summary>
    public string? InternalNotes { get; set; }

    public bool IsArchived { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public string? ArchivedByUserId { get; set; }

    public DateTime UploadedAtUtc { get; set; }
    public string? UploadedByUserId { get; set; }

    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedByUserId { get; set; }

    public string FormattedFileSize => DocumentSummaryDto.FormatFileSize(FileSizeBytes);
}

/// <summary>
/// Sanitized view for customer access (when customer visibility is implemented).
/// Strictly excludes internal notes, confidentiality levels, storage keys, and audit details.
/// </summary>
public class CustomerDocumentDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadDateUtc { get; set; }
    public string FormattedFileSize => DocumentSummaryDto.FormatFileSize(FileSizeBytes);
}

public class DocumentSearchFilterDto
{
    public int? CustomerId { get; set; }
    public int? PropertyId { get; set; }
    public int? ProcedureId { get; set; }
    public DocumentType? Type { get; set; }
    public DocumentConfidentialityLevel? ConfidentialityLevel { get; set; }
    public bool? IsCustomerVisible { get; set; }
    public bool? IsArchived { get; set; }
    public string? SearchTerm { get; set; }
}

/// <summary>
/// Result of an authorized document download request.
/// Carries the stream, MIME type, and safe download file name.
/// </summary>
public sealed class DocumentDownloadResult : IDisposable
{
    public Stream Stream { get; init; } = Stream.Null;
    public string ContentType { get; init; } = "application/octet-stream";
    public string DownloadFileName { get; init; } = "document.bin";
    public long FileSizeBytes { get; init; }

    public void Dispose()
    {
        Stream?.Dispose();
    }
}
