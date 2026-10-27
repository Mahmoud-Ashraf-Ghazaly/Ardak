namespace Ardak.Model.Enums;

public enum DocumentType
{
    /// <summary>National ID, Iqama, Commercial Register, or Passport.</summary>
    IdentityDocument = 1,

    /// <summary>Deed, Title Deed, or Sukuk.</summary>
    DeedOrTitle = 2,

    /// <summary>Land survey report, boundary demarcation, or elevation diagram.</summary>
    SurveyReport = 3,

    /// <summary>Official land legalization application (Taqneen / تقنين).</summary>
    LegalizationApplication = 4,

    /// <summary>Building violation reconciliation application (Tasaleh / تصالح).</summary>
    ReconciliationApplication = 5,

    /// <summary>Utility connection permit (electricity, water, sanitation, telecom).</summary>
    UtilityPermit = 6,

    /// <summary>Payment receipt, bank transfer slip, or treasury voucher.</summary>
    PaymentReceipt = 7,

    /// <summary>Official correspondence, decree, or ministerial letter.</summary>
    OfficialCorrespondence = 8,

    /// <summary>Power of attorney (Wakalah / وكالة شرعية).</summary>
    PowerOfAttorney = 9,

    /// <summary>Purchase agreement, sales contract, or lease contract.</summary>
    ContractOrAgreement = 10,

    /// <summary>Engineering diagram, CAD blueprint, or architectural layout.</summary>
    EngineeringPlan = 11,

    /// <summary>Other document type.</summary>
    Other = 99
}
