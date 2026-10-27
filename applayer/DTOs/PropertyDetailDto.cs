using Ardak.Model.Enums;

namespace Ardak.Application.DTOs;

public class PropertyDetailDto
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public PropertyType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string Governorate { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string DistrictOrArea { get; set; } = string.Empty;
    public string? AddressDetails { get; set; }

    public decimal AreaMeasurement { get; set; }
    public string AreaUnit { get; set; } = "sqm";

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string? OwnershipType { get; set; }
    public string? DeedOrTitleNumber { get; set; }

    public PropertyStatus Status { get; set; }

    /// <summary>
    /// Confidential internal notes - strictly for internal staff (SuperAdmin/Admin).
    /// </summary>
    public string? InternalNotes { get; set; }

    public int PrimaryCustomerId { get; set; }
    public string PrimaryCustomerName { get; set; } = string.Empty;
    public string PrimaryCustomerPhone { get; set; } = string.Empty;

    public IReadOnlyList<AssociatedCustomerDto> AssociatedCustomers { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
}
