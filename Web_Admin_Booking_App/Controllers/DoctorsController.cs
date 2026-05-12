using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;

namespace Web_Admin_Booking_App.Controllers;

public class DoctorsController : Controller
{
    public IActionResult Index()
    {
        var doctors = new List<DoctorListItemViewModel>
        {
            new() { Id = 1, FullName = "BS. Trần Đức Long", Department = "Nội tổng quát", Phone = "0901 234 567", Status = "Đang trực" },
            new() { Id = 2, FullName = "BS. Lê Minh Thư", Department = "Nhi khoa", Phone = "0902 345 678", Status = "Đang khám" },
            new() { Id = 3, FullName = "BS. Ngô Bảo Châu", Department = "Chẩn đoán hình ảnh", Phone = "0903 456 789", Status = "Nghỉ" },
            new() { Id = 4, FullName = "BS. Phạm Quang Huy", Department = "Tim mạch", Phone = "0904 567 890", Status = "Đang trực" },
        };

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

        // TODO: Persist to database
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Details(int id)
    {
        var doctor = new DoctorDetailsViewModel
        {
            Id = id,
            FullName = "BS. Trần Đức Long",
            Department = "Nội tổng quát",
            Phone = "0901 234 567",
            Email = "doctor@example.com",
            Status = "Đang trực",
        };

        return View(doctor);
    }
}
