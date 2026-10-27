using Ardak.Model.Enums;

namespace Ardak.Model.Entities;

public class Property
{
    public int Id { get; set; }

    /// <summary>
    /// Unique internal property reference number (e.g. "PRP-2026-0001").
    /// </summary>
    public string ReferenceNumber { get; set; } = string.Empty;

    public PropertyType Type { get; set; } = PropertyType.Land;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    // Location details
    public string Governorate { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string DistrictOrArea { get; set; } = string.Empty;
    public string? AddressDetails { get; set; }

    // Area measurements
    public decimal AreaMeasurement { get; set; }
    public string AreaUnit { get; set; } = "sqm";

    // Coordinates (optional)
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Ownership & deed details
    public string? OwnershipType { get; set; } // Freehold, Leasehold, Title Deed
    public string? DeedOrTitleNumber { get; set; } // Official deed / Sukuk number

    public PropertyStatus Status { get; set; } = PropertyStatus.Active;

    /// <summary>
    /// Confidential internal notes - strictly for internal staff (SuperAdmin/Admin).
    /// Kept private and never exposed to public or customer interfaces.
    /// </summary>
    public string? InternalNotes { get; set; }

    /// <summary>
    /// Primary customer associated with this property record.
    /// </summary>
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>
    /// Multiple customer associations (co-owners, investors, representatives).
    /// </summary>
    public ICollection<PropertyCustomer> CustomerAssociations { get; set; } = new List<PropertyCustomer>();

    /// <summary>
    /// Legal and administrative procedures for this property.
    /// </summary>
    public ICollection<Procedure> Procedures { get; set; } = new List<Procedure>();

    /// <summary>
    /// Documents associated with this property.
    /// </summary>
    public ICollection<Document> Documents { get; set; } = new List<Document>();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAtUtc { get; set; }
}
