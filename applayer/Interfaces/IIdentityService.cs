using Ardak.Application.DTOs;

namespace Ardak.Application.Interfaces;

public interface IIdentityService
{
    Task<IReadOnlyList<AdminUserDto>> GetAdminsAsync();
    Task<IdentityOperationResult> CreateAdminAsync(CreateAdminDto dto, string currentUserId);
    Task<IdentityOperationResult> ToggleAdminStatusAsync(string adminUserId, string currentUserId);
    Task<IdentityOperationResult> CreateCustomerAccountAsync(CreateCustomerAccountDto dto, string currentUserId);
    Task<IdentityOperationResult> ToggleCustomerAccountStatusAsync(int customerId, string currentUserId);
    Task<SignInResultDto> LoginAsync(string email, string password, bool rememberMe);
    Task LogoutAsync();
}
