using Ardak.Model.Enums;

namespace Ardak.Application.DTOs;

public class CustomerSummaryDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public CustomerStatus Status { get; set; }
    public bool HasPortalAccount { get; set; }
    public int PropertyCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
