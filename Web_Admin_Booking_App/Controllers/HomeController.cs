using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers
{
    public class HomeController : Controller
    {
        private readonly FirestoreAdminDataService _dataService;

        public HomeController(FirestoreAdminDataService dataService)
        {
            _dataService = dataService;
        }

        public async Task<IActionResult> Index(string range = "this-week", CancellationToken cancellationToken = default)
        {
            var model = await _dataService.GetDashboardAsync(range, cancellationToken);
            return View(model);
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
