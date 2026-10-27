using Ardak.Application.Interfaces;
using Ardak.Model.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace landy.Controllers;

/// <summary>
/// Authenticated Customer Portal.
/// Strictly restricted to authenticated users in the Customer role.
/// All records and files are gated by customer identity and relationship verification.
/// Read-only access to client-approved property and procedure information.
/// </summary>
[Authorize(Policy = AppPolicies.RequireCustomer)]
public class CustomerPortalController : Controller
{
    private readonly ICustomerPortalService _customerPortalService;
    private readonly IDocumentService _documentService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CustomerPortalController> _logger;

    public CustomerPortalController(
        ICustomerPortalService customerPortalService,
        IDocumentService documentService,
        ICurrentUserService currentUserService,
        ILogger<CustomerPortalController> logger)
    {
        _customerPortalService = customerPortalService;
        _documentService = documentService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    // GET: /CustomerPortal
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!_currentUserService.CustomerId.HasValue)
        {
            return Forbid();
        }

        var dashboard = await _customerPortalService.GetCustomerDashboardAsync(_currentUserService.UserId!);
        if (dashboard == null)
        {
            return NotFound("Customer record not found or access restricted.");
        }

        return View(dashboard);
    }

    // GET: /CustomerPortal/Properties
    [HttpGet]
    public async Task<IActionResult> Properties()
    {
        if (!_currentUserService.CustomerId.HasValue)
        {
            return Forbid();
        }

        var properties = await _customerPortalService.GetCustomerPropertiesAsync(_currentUserService.UserId!);
        return View(properties);
    }

    // GET: /CustomerPortal/PropertyDetails/5
    [HttpGet]
    public async Task<IActionResult> PropertyDetails(int id)
    {
        if (!_currentUserService.CustomerId.HasValue)
        {
            return Forbid();
        }

        var property = await _customerPortalService.GetCustomerPropertyDetailAsync(id, _currentUserService.UserId!);
        if (property == null)
        {
            // Return safe error response without leaking confidential data
            return NotFound("Property record not found or access restricted.");
        }

        return View(property);
    }

    // GET: /CustomerPortal/ProcedureDetails/5
    [HttpGet]
    public async Task<IActionResult> ProcedureDetails(int id)
    {
        if (!_currentUserService.CustomerId.HasValue)
        {
            return Forbid();
        }

        var procedure = await _customerPortalService.GetCustomerProcedureDetailAsync(id, _currentUserService.UserId!);
        if (procedure == null)
        {
            // Return safe error response
            return NotFound("Procedure record not found or access restricted.");
        }

        return View(procedure);
    }

    // GET: /CustomerPortal/DownloadDocument/5
    [HttpGet]
    public async Task<IActionResult> DownloadDocument(int id)
    {
        if (!_currentUserService.CustomerId.HasValue)
        {
            return Forbid();
        }

        var result = await _documentService.GetDocumentDownloadAsync(id, _currentUserService.UserId!);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Customer download denied for document ID {DocumentId} to user '{UserId}'.",
                id, _currentUserService.UserId);
            return Forbid();
        }

        var download = result.Data!;
        // Stream directly to client; storage paths and internal URLs are completely hidden
        return File(download.Stream, download.ContentType, download.DownloadFileName);
    }
}
