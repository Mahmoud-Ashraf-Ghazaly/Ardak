namespace Ardak.Application.DTOs;

public class AdminUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsPrimarySuperAdmin { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
