namespace Ardak.Application.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsSuperAdmin { get; }
    bool IsAdmin { get; }
    bool IsCustomer { get; }
    int? CustomerId { get; }
}
