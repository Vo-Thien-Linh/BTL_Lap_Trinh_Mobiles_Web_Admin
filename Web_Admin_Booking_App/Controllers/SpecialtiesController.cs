using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;

namespace Web_Admin_Booking_App.Controllers;

public class SpecialtiesController : Controller
{
    public IActionResult Index()
    {
        var model = new SpecialtyIndexViewModel
        {
            TotalCount = 24,
            ActiveCount = 22,
            NewThisMonthCount = 3,
            NewThisMonthNames = "Huyết học, Ung bướu...",
            Items = new List<SpecialtyListItemViewModel>
            {
                new()
                {
                    Id = 1,
                    Name = "Khoa Tim mạch",
                    LocationNote = "Tầng 4 - Khu A",
                    Code = "CARD-001",
                    HeadDoctor = "BS. Nguyễn Văn A",
                    DoctorCount = 12,
                    Status = SpecialtyStatus.Active,
                    Icon = SpecialtyIcon.Heart
                },
                new()
                {
                    Id = 2,
                    Name = "Khoa Thần kinh",
                    LocationNote = "Tầng 5 - Khu B",
                    Code = "NEUR-002",
                    HeadDoctor = "BS. Trần Thị B",
                    DoctorCount = 8,
                    Status = SpecialtyStatus.Active,
                    Icon = SpecialtyIcon.Brain
                },
                new()
                {
                    Id = 3,
                    Name = "Khoa Nhi",
                    LocationNote = "Tầng 2 - Khu C",
                    Code = "PED-005",
                    HeadDoctor = "BS. Lê Văn C",
                    DoctorCount = 15,
                    Status = SpecialtyStatus.Paused,
                    Icon = SpecialtyIcon.Baby
                },
                new()
                {
                    Id = 4,
                    Name = "Khoa Xét nghiệm",
                    LocationNote = "Tầng hầm 1",
                    Code = "LAB-010",
                    HeadDoctor = "BS. Phạm Minh D",
                    DoctorCount = 20,
                    Status = SpecialtyStatus.Active,
                    Icon = SpecialtyIcon.Lab
                },
            }
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new SpecialtyUpsertViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(SpecialtyUpsertViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // TODO: Persist to database
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        // TODO: Load from database
        var vm = new SpecialtyUpsertViewModel
        {
            Id = id,
            Name = "Khoa Tim mạch",
            LocationNote = "Tầng 4 - Khu A",
            Code = "CARD-001",
            HeadDoctor = "BS. Nguyễn Văn A",
            DoctorCount = 12,
            Status = SpecialtyStatus.Active,
            Icon = SpecialtyIcon.Heart
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(SpecialtyUpsertViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // TODO: Persist to database
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        // TODO: Delete from database
        return RedirectToAction(nameof(Index));
    }
}
