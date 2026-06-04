using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

[Authorize(Policy = "StaffOnly")]
public sealed class DoctorSchedulesController : Controller
{
    private readonly FirestoreAdminDataService _dataService;

    public DoctorSchedulesController(FirestoreAdminDataService dataService)
    {
        _dataService = dataService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateOnly? date,
        string? departmentId,
        string? roomNumber,
        string? shiftId,
        CancellationToken cancellationToken = default)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var model = await _dataService.GetWorkShiftsAsync("list", selectedDate, cancellationToken);

        var schedules = model.Schedules.AsEnumerable();
        schedules = schedules.Where(x => x.Date == selectedDate);

        if (!string.IsNullOrWhiteSpace(departmentId))
        {
            schedules = schedules.Where(x => string.Equals(x.DepartmentId, departmentId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(roomNumber))
        {
            schedules = schedules.Where(x => string.Equals(x.RoomNumber, roomNumber, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(shiftId))
        {
            schedules = schedules.Where(x => string.Equals(x.ShiftId, shiftId, StringComparison.OrdinalIgnoreCase));
        }

        model.Schedules = schedules
            .OrderBy(x => x.Date)
            .ThenBy(x => x.RoomNumber)
            .ThenBy(x => x.ShiftType)
            .ThenBy(x => x.DoctorName)
            .ToList();
        model.GenerateForm.StartDate = selectedDate;
        model.GenerateForm.WeeksAhead = 1;
        model.GenerateForm.AvailableSlots = 10;

        ViewData["SelectedDate"] = selectedDate;
        ViewData["DepartmentId"] = departmentId ?? string.Empty;
        ViewData["RoomNumber"] = roomNumber ?? string.Empty;
        ViewData["ShiftId"] = shiftId ?? string.Empty;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WorkScheduleGenerateViewModel model, CancellationToken cancellationToken = default)
    {
        model.WeeksAhead = 1;
        model.DaysOfWeek = new List<DayOfWeek> { model.StartDate.DayOfWeek };

        if (model.ShiftIds.Count == 0)
        {
            ModelState.AddModelError(nameof(model.ShiftIds), "Vui lòng chọn ca làm việc.");
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = string.Join(" ", ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage)
                .Where(x => !string.IsNullOrWhiteSpace(x)));
            return RedirectToAction(nameof(Index), new { date = model.StartDate.ToString("yyyy-MM-dd") });
        }

        var created = await _dataService.GenerateDoctorSchedulesAsync(model, CancellationToken.None);
        if (created == 0)
        {
            TempData["ErrorMessage"] = "Không tạo được lịch. Có thể bác sĩ hoặc phòng đã có lịch trong cùng ngày, cùng ca.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã phân công lịch bác sĩ.";
        }

        return RedirectToAction(nameof(Index), new { date = model.StartDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSchedule(
        string documentId,
        bool isActive,
        int availableSlots,
        string? roomNumber,
        DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId)) return BadRequest();

        try
        {
            await _dataService.UpdateDoctorScheduleAsync(documentId, isActive, availableSlots, roomNumber, CancellationToken.None);
            TempData["SuccessMessage"] = "Đã cập nhật lịch làm việc.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { date = (date ?? DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd") });
    }
}
