namespace Ardak.Application.DTOs;

public class CustomerProfileDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? NationalIdOrTaxNumber { get; set; }
    public bool IsActive { get; set; }
    public DateTime MemberSinceUtc { get; set; }
    public bool HasPortalAccount { get; set; }
}
