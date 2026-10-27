using Microsoft.AspNetCore.Identity;

namespace Ardak.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsPrimarySuperAdmin { get; set; } = false;
    public int? CustomerId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
