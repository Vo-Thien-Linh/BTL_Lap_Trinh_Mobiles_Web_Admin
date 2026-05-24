using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

public class DoctorsController : Controller
{
    private readonly FirestoreAdminDataService _dataService;

    public DoctorsController(FirestoreAdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var doctors = await _dataService.GetDoctorsAsync(cancellationToken);
        return View(doctors);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new DoctorCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(DoctorCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        TempData["InfoMessage"] = "Chức năng tạo bác sĩ từ Web Admin sẽ được nối Firestore ở bước tiếp theo.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var doctor = await _dataService.GetDoctorDetailsAsync(id, cancellationToken);
        if (doctor is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy bác sĩ trên Firebase.";
            return RedirectToAction(nameof(Index));
        }

        return View(doctor);
    }
}
