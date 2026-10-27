namespace Ardak.Model.Constants;

public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Customer = "Customer";

    public static readonly IReadOnlyList<string> AdministrativeRoles = [SuperAdmin, Admin];
}
