namespace Ardak.Infrastructure.Identity;

public class SuperAdminOptions
{
    public const string SectionName = "SuperAdmin";

    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = "Primary SuperAdmin";
}
