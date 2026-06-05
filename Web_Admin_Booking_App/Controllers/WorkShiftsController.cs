using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

[Authorize(Policy = "AdminOnly")]
public class WorkShiftsController : Controller
{
    private readonly FirestoreAdminDataService _dataService;

    public WorkShiftsController(FirestoreAdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<IActionResult> Index(string? mode = null, DateOnly? weekStart = null, string? doctorId = null, CancellationToken cancellationToken = default)
    {
        mode ??= "calendar";
        var model = await _dataService.GetWorkShiftsAsync(mode, weekStart, doctorId, CancellationToken.None);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EditSchedule(string id, CancellationToken cancellationToken = default)
    {
        var model = await _dataService.GetDoctorScheduleEditAsync(id, CancellationToken.None);
        if (model is null)
        {
            TempData["InfoMessage"] = "Không tìm thấy lịch làm việc cần sửa.";
            return RedirectToAction(nameof(Index), new { mode = "list" });
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSchedule(WorkScheduleEditViewModel model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            var existing = await _dataService.GetDoctorScheduleEditAsync(model.DocumentId, CancellationToken.None);
            if (existing is not null)
            {
                model.Doctors = existing.Doctors;
                model.Departments = existing.Departments;
                model.DepartmentRooms = existing.DepartmentRooms;
                model.Shifts = existing.Shifts;
            }

            return View(model);
        }

        try
        {
            await _dataService.UpdateDoctorScheduleAsync(model, CancellationToken.None);
            TempData["InfoMessage"] = "Đã cập nhật lịch làm việc.";
            return RedirectToAction(nameof(Index), new { mode = "list", weekStart = model.ScheduleDate.ToString("yyyy-MM-dd"), doctorId = model.DoctorId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var existing = await _dataService.GetDoctorScheduleEditAsync(model.DocumentId, CancellationToken.None);
            if (existing is not null)
            {
                model.Doctors = existing.Doctors;
                model.Departments = existing.Departments;
                model.DepartmentRooms = existing.DepartmentRooms;
                model.Shifts = existing.Shifts;
            }

            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(WorkScheduleGenerateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["InfoMessage"] = string.Join(" ", ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage)
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            return RedirectToAction(nameof(Index));
        }

        try
        {
            var created = await _dataService.GenerateDoctorSchedulesAsync(model, CancellationToken.None);
            TempData["InfoMessage"] = created > 0
                ? $"Đã sinh {created} lịch làm việc mới."
                : "Không có lịch mới được tạo.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["InfoMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new
        {
            mode = "calendar",
            weekStart = model.StartDate.ToString("yyyy-MM-dd"),
            doctorId = model.DoctorId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSchedule(string documentId, bool isActive, int availableSlots, string? roomNumber, string? doctorId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            TempData["InfoMessage"] = "Không xác định được lịch cần cập nhật.";
            return RedirectToAction(nameof(Index), new { mode = "list", doctorId });
        }

        if (string.IsNullOrWhiteSpace(roomNumber))
        {
            TempData["InfoMessage"] = "Vui lòng nhập phòng khám.";
            return RedirectToAction(nameof(Index), new { mode = "list", doctorId });
        }

        if (availableSlots < 0 || availableSlots > 100)
        {
            TempData["InfoMessage"] = "Số slot khả dụng phải từ 0 đến 100.";
            return RedirectToAction(nameof(Index), new { mode = "list", doctorId });
        }

        try
        {
            await _dataService.UpdateDoctorScheduleAsync(documentId, isActive, availableSlots, roomNumber, CancellationToken.None);
            TempData["InfoMessage"] = "Đã cập nhật lịch làm việc.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["InfoMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { mode = "list", doctorId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DuplicateSchedule(string sourceScheduleId, DateOnly targetDate, string? doctorId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceScheduleId))
        {
            TempData["InfoMessage"] = "Không xác định được lịch mẫu cần bổ sung.";
            return RedirectToAction(nameof(Index), new { mode = "list", doctorId });
        }

        try
        {
            await _dataService.DuplicateDoctorScheduleAsync(sourceScheduleId, targetDate, CancellationToken.None);
            TempData["InfoMessage"] = $"Đã bổ sung lịch làm việc ngày {targetDate:dd/MM/yyyy}.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["InfoMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { mode = "list", weekStart = targetDate.ToString("yyyy-MM-dd"), doctorId });
    }
}
