namespace Ardak.Model.Enums;

public enum DocumentConfidentialityLevel
{
    /// <summary>Standard document. Eligible to be made customer-visible if explicitly approved.</summary>
    Standard = 1,

    /// <summary>Internal staff use only. Not for general distribution.</summary>
    Internal = 2,

    /// <summary>Confidential operational, legal, or financial record.</summary>
    Confidential = 3,

    /// <summary>Strictly confidential document (executive or legal counsel only).</summary>
    StrictlyConfidential = 4
}
