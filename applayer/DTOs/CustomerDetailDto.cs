using Ardak.Model.Enums;

namespace Ardak.Application.DTOs;

public class CustomerDetailDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? NationalIdOrTaxNumber { get; set; }
    public CustomerStatus Status { get; set; }
    
    /// <summary>
    /// Confidential internal notes - visible only to internal staff (SuperAdmin/Admin).
    /// </summary>
    public string? InternalNotes { get; set; }

    public string? IdentityUserId { get; set; }
    public bool HasPortalAccount => !string.IsNullOrEmpty(IdentityUserId);

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }

    public int PropertyCount { get; set; }
    public IReadOnlyList<string> PropertyTitles { get; set; } = [];
}
