using Ardak.Model.Enums;

namespace Ardak.Model.Entities;

/// <summary>
/// Represents a legal or administrative procedure associated with a property.
/// Each property may have many independent procedures (e.g. Taqneen, Tasaleh, utilities).
/// </summary>
public class Procedure
{
    public int Id { get; set; }

    // ── Relationship ────────────────────────────────────────────────
    public int PropertyId { get; set; }
    public Property? Property { get; set; }

    // ── Classification ──────────────────────────────────────────────
    public ProcedureType Type { get; set; } = ProcedureType.Other;

    /// <summary>Free-text label when Type == Other, or a sub-type qualifier.</summary>
    public string? TypeLabel { get; set; }

    // ── Reference & Authority ───────────────────────────────────────
    /// <summary>Application or case reference number issued by the authority.</summary>
    public string? ApplicationNumber { get; set; }

    /// <summary>Name of the government authority or department handling the procedure.</summary>
    public string? ResponsibleAuthority { get; set; }

    // ── Status & Timeline ───────────────────────────────────────────
    public ProcedureStatus Status { get; set; } = ProcedureStatus.Submitted;

    public DateTime? SubmissionDate { get; set; }

    /// <summary>Next action deadline for the team (internal tracker).</summary>
    public DateTime? NextActionDeadline { get; set; }

    /// <summary>Description of the next required action.</summary>
    public string? NextActionDescription { get; set; }

    // ── Missing Documents ───────────────────────────────────────────
    /// <summary>
    /// Comma-separated or free-text list of documents still outstanding.
    /// Kept internal; never surfaced to customers.
    /// </summary>
    public string? MissingDocuments { get; set; }

    // ── Financial Tracking ──────────────────────────────────────────
    /// <summary>Total government or authority fee for this procedure.</summary>
    public decimal? TotalFee { get; set; }

    /// <summary>Amount already paid.</summary>
    public decimal? AmountPaid { get; set; }

    /// <summary>Remaining balance (computed, stored for search convenience).</summary>
    public decimal? BalanceDue { get; set; }

    // ── Internal Notes ──────────────────────────────────────────────
    /// <summary>
    /// Confidential staff notes. Must never be exposed to customers.
    /// </summary>
    public string? InternalNotes { get; set; }

    // ── Customer Visibility ─────────────────────────────────────────
    /// <summary>
    /// Explicit flag indicating whether the procedure status and approved update notes
    /// are visible to the customer through the portal.
    /// MUST be false by default.
    /// </summary>
    public bool IsCustomerVisible { get; set; } = false;

    /// <summary>
    /// Official, client-approved progress notes.
    /// Only visible to customers when IsCustomerVisible is true.
    /// Internal notes, fees, and missing documents remain strictly confidential.
    /// </summary>
    public string? CustomerUpdateNotes { get; set; }

    // ── Audit ───────────────────────────────────────────────────────
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAtUtc { get; set; }

    // ── Status History ──────────────────────────────────────────────
    public ICollection<ProcedureStatusHistory> StatusHistory { get; set; } = new List<ProcedureStatusHistory>();

    /// <summary>
    /// Documents associated with this procedure.
    /// </summary>
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
