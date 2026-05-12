using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;

namespace Web_Admin_Booking_App.Controllers;

public class PatientsController : Controller
{
    public IActionResult Index()
    {
        var patients = new List<PatientListItemViewModel>
        {
            new() { Id = 3001, FullName = "Nguyễn Văn An", Phone = "0901 234 567", Gender = Gender.Male, Dob = new DateOnly(1996, 5, 12), Status = PatientStatus.Active },
            new() { Id = 3002, FullName = "Phạm Thị Bích", Phone = "0902 345 678", Gender = Gender.Female, Dob = new DateOnly(1998, 2, 20), Status = PatientStatus.Active },
            new() { Id = 3003, FullName = "Trần Hoàng Nam", Phone = "0903 456 789", Gender = Gender.Male, Dob = new DateOnly(1992, 11, 3), Status = PatientStatus.Inactive },
            new() { Id = 3004, FullName = "Đỗ Tuyết Mai", Phone = "0904 567 890", Gender = Gender.Female, Dob = new DateOnly(2000, 8, 19), Status = PatientStatus.Active },
        };

        return View(patients);
    }

    public IActionResult Details(int id)
    {
        var model = new PatientDetailsViewModel
        {
            Id = id,
            FullName = "Nguyễn Văn An",
            Phone = "0901 234 567",
            Email = "patient@example.com",
            Gender = Gender.Male,
            Dob = new DateOnly(1996, 5, 12),
            Address = "Quận 1, TP. Hồ Chí Minh",
            Status = PatientStatus.Active,
            LastVisitAt = DateTime.Today.AddDays(-3).AddHours(10).AddMinutes(15),
            Notes = "Dị ứng: Không. Tiền sử: Tăng huyết áp nhẹ."
        };

        return View(model);
    }
}
