using Ardak.Model.Enums;

namespace Ardak.Application.DTOs;

public class PropertySearchFilterDto
{
    public string? ReferenceNumber { get; set; }
    public string? Location { get; set; }
    public PropertyType? Type { get; set; }
    public PropertyStatus? Status { get; set; }
    public int? CustomerId { get; set; }
}
