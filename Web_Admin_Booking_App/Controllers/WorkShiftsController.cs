using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

[Authorize(Policy = "AdminOnly")]
public class WorkShiftsController : Controller
{
    private readonly FirestoreAdminDataService _dataService;

    public WorkShiftsController(FirestoreAdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<IActionResult> Index(DateOnly? weekStart = null, CancellationToken cancellationToken = default)
    {
        var model = await _dataService.GetWorkShiftsAsync("calendar", weekStart, null, CancellationToken.None);
        return View(model);
    }
}
