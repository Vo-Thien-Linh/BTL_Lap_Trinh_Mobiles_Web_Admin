using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

public class PatientsController : Controller
{
    private readonly FirestoreAdminDataService _dataService;

    public PatientsController(FirestoreAdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var patients = await _dataService.GetPatientsAsync(cancellationToken);
        return View(patients);
    }

    public IActionResult Details(int id)
    {
        return RedirectToAction(nameof(Index));
    }
}
