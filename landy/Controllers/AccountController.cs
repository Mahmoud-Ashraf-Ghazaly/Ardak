using Ardak.Application.Interfaces;
using Ardak.Model.Constants;
using landy.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace landy.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IIdentityService identityService,
        ICurrentUserService currentUserService,
        ILogger<AccountController> logger)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_currentUserService.IsAuthenticated)
        {
            return RedirectToDefaultArea();
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _identityService.LoginAsync(model.Email, model.Password, model.RememberMe);

        if (result.Succeeded)
        {
            _logger.LogInformation("User '{Email}' logged in successfully.", model.Email);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToDefaultArea();
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty, "This account is inactive or not authorized to log in.");
            return View(model);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "This account has been temporarily locked out due to multiple failed login attempts.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Invalid login attempt.");
        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _identityService.LogoutAsync();
        _logger.LogInformation("User logged out.");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToDefaultArea()
    {
        if (_currentUserService.IsSuperAdmin || _currentUserService.IsAdmin)
        {
            return RedirectToAction("Index", "InternalDashboard");
        }

        if (_currentUserService.IsCustomer)
        {
            return RedirectToAction("Index", "CustomerPortal");
        }

        return RedirectToAction("Index", "Home");
    }
}
