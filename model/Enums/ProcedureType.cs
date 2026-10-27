namespace Ardak.Model.Enums;

public enum ProcedureType
{
    /// <summary>Land legalization (Taqneen / تقنين).</summary>
    Taqneen = 1,

    /// <summary>Reconciliation (Tasaleh / تصالح).</summary>
    Tasaleh = 2,

    /// <summary>Utility and service connections (electricity, water, etc.).</summary>
    UtilityConnection = 3,

    /// <summary>Title deed issuance or transfer.</summary>
    TitleDeedIssuance = 4,

    /// <summary>Subdivision or parcel splitting.</summary>
    Subdivision = 5,

    /// <summary>Boundary demarcation survey.</summary>
    BoundaryDemarcation = 6,

    /// <summary>Any other procedure type not listed above.</summary>
    Other = 99
}
