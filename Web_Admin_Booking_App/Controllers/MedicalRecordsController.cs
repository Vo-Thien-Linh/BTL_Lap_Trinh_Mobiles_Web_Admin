using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;

namespace Web_Admin_Booking_App.Controllers;

public class MedicalRecordsController : Controller
{
    public IActionResult Index()
    {
        var model = new List<MedicalRecordListItemViewModel>
        {
            new()
            {
                Id = 2001,
                RecordCode = "BA-8821",
                PatientName = "Phạm Văn Nam",
                DoctorName = "BS. Nguyễn Văn A",
                SpecialtyName = "Tim mạch",
                CreatedAt = DateTime.Today.AddDays(-2).AddHours(9),
                Status = MedicalRecordStatus.Approved
            },
            new()
            {
                Id = 2002,
                RecordCode = "BA-8822",
                PatientName = "Trần Hồng Hạnh",
                DoctorName = "BS. Trần Thị B",
                SpecialtyName = "Nội tổng quát",
                CreatedAt = DateTime.Today.AddDays(-1).AddHours(10),
                Status = MedicalRecordStatus.Pending
            },
            new()
            {
                Id = 2003,
                RecordCode = "BA-8823",
                PatientName = "Lê Minh Quân",
                DoctorName = "BS. Lê Hoàng C",
                SpecialtyName = "Răng - Hàm - Mặt",
                CreatedAt = DateTime.Today.AddDays(-1).AddHours(14),
                Status = MedicalRecordStatus.Cancelled
            },
            new()
            {
                Id = 2004,
                RecordCode = "BA-8824",
                PatientName = "Đỗ Tuyết Mai",
                DoctorName = "BS. Nguyễn Văn A",
                SpecialtyName = "Xét nghiệm",
                CreatedAt = DateTime.Today.AddHours(11),
                Status = MedicalRecordStatus.Approved
            },
        };

        return View(model);
    }

    public IActionResult Details(int id)
    {
        var model = new MedicalRecordDetailsViewModel
        {
            Id = id,
            RecordCode = $"BA-{id}",
            PatientName = "Phạm Văn Nam",
            PatientPhone = "0901 234 567",
            DoctorName = "BS. Nguyễn Văn A",
            SpecialtyName = "Tim mạch",
            CreatedAt = DateTime.Today.AddDays(-2).AddHours(9),
            Status = MedicalRecordStatus.Approved,
            Diagnosis = "Theo dõi tăng huyết áp.",
            ClinicalNotes = "Bệnh nhân ổn định, không khó thở.",
            Prescription = "Amlodipine 5mg - 1 viên/ngày.",
        };

        return View(model);
    }
}
