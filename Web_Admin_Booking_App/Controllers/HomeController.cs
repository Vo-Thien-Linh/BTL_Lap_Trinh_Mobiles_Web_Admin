using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Grpc.Core;
using System.Diagnostics;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers
{
    public class HomeController : Controller
    {
        private readonly FirestoreAdminDataService _dataService;
        private readonly IMemoryCache _cache;

        public HomeController(FirestoreAdminDataService dataService, IMemoryCache cache)
        {
            _dataService = dataService;
            _cache = cache;
        }

        public async Task<IActionResult> Index(string range = "this-week", CancellationToken cancellationToken = default)
        {
            var cacheKey = $"dashboard:{range}";
            try
            {
                var model = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                    return await _dataService.GetDashboardAsync(range, cancellationToken);
                });

                return View(model ?? new DashboardViewModel());
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.ResourceExhausted)
            {
                TempData["ErrorMessage"] = "Firestore đang vượt quota đọc dữ liệu. Vui lòng thử lại sau.";
                return View(new DashboardViewModel());
            }
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
