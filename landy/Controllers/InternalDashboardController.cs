using Ardak.Application.Interfaces;
using Ardak.Model.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace landy.Controllers;

/// <summary>
/// Internal Admin Dashboard for executive & operational management.
/// Only authorized staff (SuperAdmin and Admin) may access this controller.
/// Financial and legal details are strictly restricted to authorized administrators.
/// </summary>
[Authorize(Policy = AppPolicies.RequireAdminOrSuperAdmin)]
public class InternalDashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<InternalDashboardController> _logger;

    public InternalDashboardController(
        IDashboardService dashboardService,
        ICurrentUserService currentUserService,
        ILogger<InternalDashboardController> logger)
    {
        _dashboardService = dashboardService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var dashboardData = await _dashboardService.GetDashboardMetricsAsync();

        ViewBag.IsSuperAdmin = _currentUserService.IsSuperAdmin;
        ViewBag.UserEmail = _currentUserService.Email;
        ViewBag.UserId = _currentUserService.UserId;

        return View(dashboardData);
    }
}
