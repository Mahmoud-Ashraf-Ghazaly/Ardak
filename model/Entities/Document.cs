using Ardak.Model.Enums;

namespace Ardak.Model.Entities;

/// <summary>
/// Represents a managed document associated with a Customer, Property, or Procedure.
/// Physical files are stored in a private, non-web-accessible storage location.
/// Access requires explicit server-side authorization on every operation.
/// </summary>
public class Document
{
    public int Id { get; set; }

    // ── Associations ────────────────────────────────────────────────────────
    /// <summary>Associated Customer (optional).</summary>
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Associated Property (optional).</summary>
    public int? PropertyId { get; set; }
    public Property? Property { get; set; }

    /// <summary>Associated Legal or Administrative Procedure (optional).</summary>
    public int? ProcedureId { get; set; }
    public Procedure? Procedure { get; set; }

    // ── Document Metadata ───────────────────────────────────────────────────
    public DocumentType Type { get; set; } = DocumentType.Other;

    /// <summary>Free-text description or subtype qualifier when Type == Other.</summary>
    public string? TypeLabel { get; set; }

    /// <summary>Descriptive title for the document.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional overview or details regarding this document.</summary>
    public string? Description { get; set; }

    /// <summary>Official reference / certificate / deed / permit number when applicable.</summary>
    public string? ReferenceNumber { get; set; }

    // ── File Metadata & Storage ─────────────────────────────────────────────
    /// <summary>Original file name as provided upon upload.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Opaque storage key (GUID-based token).
    /// Physical paths, storage credentials, or private URLs are never exposed.
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>MIME content type (e.g. application/pdf, image/jpeg).</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>Normalized file extension with leading dot (e.g. .pdf).</summary>
    public string FileExtension { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the file content for data integrity verification.</summary>
    public string? ChecksumSha256 { get; set; }

    // ── Privacy & Visibility ────────────────────────────────────────────────
    public DocumentConfidentialityLevel ConfidentialityLevel { get; set; } = DocumentConfidentialityLevel.Internal;

    /// <summary>
    /// Explicit flag determining customer portal visibility.
    /// MUST be false by default. All documents are private until explicitly approved.
    /// </summary>
    public bool IsCustomerVisible { get; set; } = false;

    /// <summary>
    /// Staff-only internal notes. Strictly confidential; never exposed to customers.
    /// </summary>
    public string? InternalNotes { get; set; }

    // ── Historical Record & Archiving ───────────────────────────────────────
    /// <summary>
    /// Indicates whether the document has been archived.
    /// Historical documents are preserved rather than destroyed.
    /// </summary>
    public bool IsArchived { get; set; } = false;

    public DateTime? ArchivedAtUtc { get; set; }
    public string? ArchivedByUserId { get; set; }

    // ── Audit ───────────────────────────────────────────────────────────────
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public string? UploadedByUserId { get; set; }

    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedByUserId { get; set; }
}
