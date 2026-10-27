using Ardak.Model.Enums;

namespace Ardak.Model.Entities;

/// <summary>
/// Immutable audit record of a status change on a <see cref="Procedure"/>.
/// Written whenever a procedure's status is updated.
/// </summary>
public class ProcedureStatusHistory
{
    public int Id { get; set; }

    public int ProcedureId { get; set; }
    public Procedure? Procedure { get; set; }

    public ProcedureStatus FromStatus { get; set; }
    public ProcedureStatus ToStatus { get; set; }

    /// <summary>Optional explanation for the status change.</summary>
    public string? ChangeNote { get; set; }

    /// <summary>Identity user ID of the staff member who made the change.</summary>
    public string? ChangedByUserId { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
