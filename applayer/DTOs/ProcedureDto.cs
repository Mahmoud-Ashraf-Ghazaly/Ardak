using Ardak.Model.Enums;

namespace Ardak.Application.DTOs;

/// <summary>
/// Summary row for procedure listings (e.g., on the property detail page).
/// Does NOT include internal notes or financial details (safe for restricted views).
/// </summary>
public class ProcedureSummaryDto
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string PropertyReferenceNumber { get; set; } = string.Empty;
    public string PropertyTitle { get; set; } = string.Empty;
    public ProcedureType Type { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public string? ApplicationNumber { get; set; }
    public string? ResponsibleAuthority { get; set; }
    public ProcedureStatus Status { get; set; }
    public DateTime? SubmissionDate { get; set; }
    public DateTime? NextActionDeadline { get; set; }
    public string? NextActionDescription { get; set; }
    public bool HasOverdueDeadline => NextActionDeadline.HasValue && NextActionDeadline.Value.Date < DateTime.UtcNow.Date;
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Full internal detail view for authorized staff.
/// Includes confidential fields: InternalNotes, financial data, and status history.
/// </summary>
public class ProcedureDetailDto
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string PropertyReferenceNumber { get; set; } = string.Empty;
    public string PropertyTitle { get; set; } = string.Empty;

    public ProcedureType Type { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public string? ApplicationNumber { get; set; }
    public string? ResponsibleAuthority { get; set; }

    public ProcedureStatus Status { get; set; }
    public DateTime? SubmissionDate { get; set; }
    public DateTime? NextActionDeadline { get; set; }
    public string? NextActionDescription { get; set; }
    public bool HasOverdueDeadline => NextActionDeadline.HasValue && NextActionDeadline.Value.Date < DateTime.UtcNow.Date;

    public string? MissingDocuments { get; set; }

    // Financials (internal only)
    public decimal? TotalFee { get; set; }
    public decimal? AmountPaid { get; set; }
    public decimal? BalanceDue { get; set; }

    public string? InternalNotes { get; set; }

    public bool IsCustomerVisible { get; set; }
    public string? CustomerUpdateNotes { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }

    public IReadOnlyList<ProcedureStatusHistoryDto> StatusHistory { get; set; } = [];
}

/// <summary>One entry in the status change audit log.</summary>
public class ProcedureStatusHistoryDto
{
    public int Id { get; set; }
    public ProcedureStatus FromStatus { get; set; }
    public ProcedureStatus ToStatus { get; set; }
    public string? ChangeNote { get; set; }
    public string? ChangedByUserId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}

public class ProcedureSearchFilterDto
{
    public int? PropertyId { get; set; }
    public ProcedureType? Type { get; set; }
    public ProcedureStatus? Status { get; set; }
    public bool? OverdueOnly { get; set; }
}
