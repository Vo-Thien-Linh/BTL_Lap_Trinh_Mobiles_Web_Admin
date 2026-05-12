using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;

namespace Web_Admin_Booking_App.Controllers;

public class WorkShiftsController : Controller
{
    public IActionResult Index(string? mode = null)
    {
        mode ??= "calendar";

        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var calendar = CalendarBuilder.BuildMonth(monthStart);

        // Demo events placed on a few days
        CalendarBuilder.AddShift(calendar, monthStart.AddDays(1), ShiftType.Morning, "S: BS. Minh", "P.302 - Khoa Nội");
        CalendarBuilder.AddShift(calendar, monthStart.AddDays(1), ShiftType.Afternoon, "C: BS. Hằng", "P.101 - Nhi");
        CalendarBuilder.AddShift(calendar, monthStart.AddDays(2), ShiftType.Morning, "S: BS. Tuấn", "P.405 - Ngoại");
        CalendarBuilder.AddShift(calendar, monthStart.AddDays(2), ShiftType.Night, "T: BS. Linh", "Cấp cứu");
        CalendarBuilder.AddShift(calendar, monthStart.AddDays(3), ShiftType.Afternoon, "C: BS. Quang", "P.202 - Tim mạch");
        CalendarBuilder.AddShift(calendar, monthStart.AddDays(4), ShiftType.Morning, "S: BS. Lan", "P.501 - Da liễu");
        CalendarBuilder.AddShift(calendar, monthStart.AddDays(5), ShiftType.Night, "T: BS. Hải", "Trực đêm nội trú");

        var model = new WorkShiftsIndexViewModel
        {
            Mode = mode,
            RangeLabel = $"{monthStart:dd/MM} - {monthEnd:dd/MM/yyyy}",
            TotalDoctorsLabel = "42 Bác sĩ",
            UnassignedCount = 3,
            FillRatePercent = 98.2m,
            ConflictCount = 0,
            MonthLabel = $"Tháng {monthStart.Month}, {monthStart.Year}",
            Calendar = calendar,
            TodayLabel = $"Phân bổ nhân sự hôm nay ({today:dd/MM})",
            TodayAssignments = new List<TodayAssignmentViewModel>
            {
                new()
                {
                    Initial = "T",
                    DoctorName = "BS. Nguyễn Anh Tuấn",
                    DoctorTitle = "Chuyên khoa II",
                    Specialty = "Khoa Ngoại",
                    ShiftLabel = "Ca Sáng (07:30 - 11:30)",
                    Room = "Phòng 405",
                    Status = AssignmentStatus.Assigned
                },
                new()
                {
                    Initial = "L",
                    DoctorName = "BS. Trần Khánh Linh",
                    DoctorTitle = "Thạc sĩ, Bác sĩ",
                    Specialty = "Cấp cứu",
                    ShiftLabel = "Ca Tối (18:00 - 06:00)",
                    Room = "Phòng Cấp cứu",
                    Status = AssignmentStatus.Pending
                },
            }
        };

        return View(model);
    }
}
