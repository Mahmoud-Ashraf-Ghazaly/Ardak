using Ardak.Model.Enums;

namespace Ardak.Application.DTOs;

public class PropertySummaryDto
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public PropertyType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Governorate { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string DistrictOrArea { get; set; } = string.Empty;
    public decimal AreaMeasurement { get; set; }
    public string AreaUnit { get; set; } = "sqm";
    public PropertyStatus Status { get; set; }
    public int PrimaryCustomerId { get; set; }
    public string PrimaryCustomerName { get; set; } = string.Empty;
    public int CoOwnersCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
