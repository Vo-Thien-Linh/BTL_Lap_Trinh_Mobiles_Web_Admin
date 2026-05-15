using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

public class PaymentsController : Controller
{
    private readonly FirestoreAdminDataService _dataService;

    public PaymentsController(FirestoreAdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var vm = await _dataService.GetPaymentsAsync(cancellationToken);
        return View(vm);
    }
}
