namespace Ardak.Model.Enums;

public enum ProcedureStatus
{
    /// <summary>Procedure has been submitted to the authority.</summary>
    Submitted = 1,

    /// <summary>Procedure is actively being processed.</summary>
    InProgress = 2,

    /// <summary>Waiting for a document, payment, or external action.</summary>
    PendingRequirements = 3,

    /// <summary>Under review by the responsible authority.</summary>
    UnderReview = 4,

    /// <summary>Procedure has been approved.</summary>
    Approved = 5,

    /// <summary>Procedure was rejected; may be re-submitted.</summary>
    Rejected = 6,

    /// <summary>Procedure was fully completed.</summary>
    Completed = 7,

    /// <summary>Procedure was cancelled.</summary>
    Cancelled = 8
}
