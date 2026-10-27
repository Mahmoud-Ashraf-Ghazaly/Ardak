using Ardak.Model.Enums;

namespace Ardak.Application.DTOs;

/// <summary>
/// Root dashboard view for an authenticated customer.
/// Contains strictly client-approved records. Confidential internal notes,
/// financials, and staff tracking are entirely excluded.
/// </summary>
public class CustomerPortalDashboardDto
{
    public CustomerProfileDto Profile { get; set; } = new();
    public IReadOnlyList<CustomerPropertySummaryDto> Properties { get; set; } = [];
    public IReadOnlyList<CustomerProcedureDto> ApprovedProcedures { get; set; } = [];
    public IReadOnlyList<CustomerDocumentDto> ApprovedDocuments { get; set; } = [];
}

/// <summary>
/// Summary view of a property owned by or associated with the authenticated customer.
/// </summary>
public class CustomerPropertySummaryDto
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public PropertyType Type { get; set; }
    public string City { get; set; } = string.Empty;
    public string DistrictOrArea { get; set; } = string.Empty;
    public decimal AreaMeasurement { get; set; }
    public string AreaUnit { get; set; } = "sqm";
    public PropertyStatus Status { get; set; }

    /// <summary>E.g. "Primary Owner", "Co-Owner", "Investor".</summary>
    public string OwnershipRole { get; set; } = "Primary Owner";
    public decimal? OwnershipPercentage { get; set; }

    public int ApprovedProceduresCount { get; set; }
    public int ApprovedDocumentsCount { get; set; }
}

/// <summary>
/// Approved property details for the customer.
/// Excludes InternalNotes and company operational data.
/// </summary>
public class CustomerPropertyDetailDto
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PropertyType Type { get; set; }

    public string Governorate { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string DistrictOrArea { get; set; } = string.Empty;
    public string? AddressDetails { get; set; }

    public decimal AreaMeasurement { get; set; }
    public string AreaUnit { get; set; } = "sqm";

    public string? OwnershipType { get; set; }
    public string? DeedOrTitleNumber { get; set; }
    public PropertyStatus Status { get; set; }

    public string OwnershipRole { get; set; } = "Primary Owner";
    public decimal? OwnershipPercentage { get; set; }

    public IReadOnlyList<CustomerProcedureDto> ApprovedProcedures { get; set; } = [];
    public IReadOnlyList<CustomerDocumentDto> ApprovedDocuments { get; set; } = [];
}

/// <summary>
/// Approved procedure details for customer tracking.
/// Only exposed when IsCustomerVisible is explicitly true.
/// Excludes fees, payments, balances, internal notes, and missing document tracking.
/// </summary>
public class CustomerProcedureDto
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

    /// <summary>Approved client-facing progress notes.</summary>
    public string? CustomerUpdateNotes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public IReadOnlyList<CustomerDocumentDto> ApprovedDocuments { get; set; } = [];
}
