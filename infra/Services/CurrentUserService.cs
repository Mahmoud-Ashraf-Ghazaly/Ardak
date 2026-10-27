using System.Security.Claims;
using Ardak.Application.Interfaces;
using Ardak.Model.Constants;
using Microsoft.AspNetCore.Http;

namespace Ardak.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsSuperAdmin => User?.IsInRole(AppRoles.SuperAdmin) ?? false;

    public bool IsAdmin => User?.IsInRole(AppRoles.Admin) ?? false;

    public bool IsCustomer => User?.IsInRole(AppRoles.Customer) ?? false;

    public int? CustomerId
    {
        get
        {
            var customerIdClaim = User?.FindFirst("CustomerId")?.Value;
            return int.TryParse(customerIdClaim, out var id) ? id : null;
        }
    }
}
