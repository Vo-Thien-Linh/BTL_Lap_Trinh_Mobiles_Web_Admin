using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;

namespace Web_Admin_Booking_App.Controllers;

public class AppointmentsController : Controller
{
    public IActionResult Index()
    {
        var items = new List<AppointmentListItemViewModel>
        {
            new()
            {
                Id = 1001,
                PatientName = "Nguyễn Văn An",
                PatientNote = "Long",
                DoctorName = "BS. Trần Đức Long",
                SpecialtyName = "Nội tổng quát",
                ScheduledAt = DateTime.Today.AddHours(9).AddMinutes(30),
                Status = AppointmentStatus.Upcoming
            },
            new()
            {
                Id = 1002,
                PatientName = "Phạm Thị Bích",
                PatientNote = string.Empty,
                DoctorName = "BS. Lê Minh Thư",
                SpecialtyName = "Nhi khoa",
                ScheduledAt = DateTime.Today.AddHours(10).AddMinutes(15),
                Status = AppointmentStatus.InProgress
            },
            new()
            {
                Id = 1003,
                PatientName = "Trần Hoàng Nam",
                PatientNote = string.Empty,
                DoctorName = "BS. Ngô Bảo Châu",
                SpecialtyName = "Chẩn đoán hình ảnh",
                ScheduledAt = DateTime.Today.AddHours(11),
                Status = AppointmentStatus.Completed
            },
            new()
            {
                Id = 1004,
                PatientName = "Lê Ngọc Mai",
                PatientNote = string.Empty,
                DoctorName = "BS. Phạm Quang Huy",
                SpecialtyName = "Tim mạch",
                ScheduledAt = DateTime.Today.AddDays(1).AddHours(14),
                Status = AppointmentStatus.Confirmed
            }
        };

        return View(items);
    }

    [HttpGet]
    public IActionResult Create()
    {
        var vm = new AppointmentCreateViewModel
        {
            Date = DateOnly.FromDateTime(DateTime.Today),
            Time = new TimeOnly(9, 0),
            Status = AppointmentStatus.Pending,
            Doctors = new List<SelectOption>
            {
                new() { Value = "BS. Trần Đức Long", Text = "BS. Trần Đức Long" },
                new() { Value = "BS. Lê Minh Thư", Text = "BS. Lê Minh Thư" },
                new() { Value = "BS. Ngô Bảo Châu", Text = "BS. Ngô Bảo Châu" },
                new() { Value = "BS. Phạm Quang Huy", Text = "BS. Phạm Quang Huy" },
            },
            Specialties = new List<SelectOption>
            {
                new() { Value = "Nội tổng quát", Text = "Nội tổng quát" },
                new() { Value = "Nhi khoa", Text = "Nhi khoa" },
                new() { Value = "Tim mạch", Text = "Tim mạch" },
                new() { Value = "Chẩn đoán hình ảnh", Text = "Chẩn đoán hình ảnh" },
            }
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(AppointmentCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            // Refill dropdown options (demo only)
            model.Doctors = new List<SelectOption>
            {
                new() { Value = "BS. Trần Đức Long", Text = "BS. Trần Đức Long" },
                new() { Value = "BS. Lê Minh Thư", Text = "BS. Lê Minh Thư" },
                new() { Value = "BS. Ngô Bảo Châu", Text = "BS. Ngô Bảo Châu" },
                new() { Value = "BS. Phạm Quang Huy", Text = "BS. Phạm Quang Huy" },
            };
            model.Specialties = new List<SelectOption>
            {
                new() { Value = "Nội tổng quát", Text = "Nội tổng quát" },
                new() { Value = "Nhi khoa", Text = "Nhi khoa" },
                new() { Value = "Tim mạch", Text = "Tim mạch" },
                new() { Value = "Chẩn đoán hình ảnh", Text = "Chẩn đoán hình ảnh" },
            };

            return View(model);
        }

        // TODO: Persist to database
        return RedirectToAction(nameof(Index));
    }
}
