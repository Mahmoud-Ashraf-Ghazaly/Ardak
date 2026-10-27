using Ardak.Model.Constants;
using landy.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace landy.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole(AppRoles.SuperAdmin) || User.IsInRole(AppRoles.Admin))
                {
                    return RedirectToAction("Index", "InternalDashboard");
                }
                if (User.IsInRole(AppRoles.Customer))
                {
                    return RedirectToAction("Index", "CustomerPortal");
                }
            }

            return RedirectToAction("Login", "Account");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
