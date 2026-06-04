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

    public async Task<IActionResult> Index(string? mode = null, DateOnly? weekStart = null, CancellationToken cancellationToken = default)
    {
        var model = await _dataService.GetWorkShiftsAsync("calendar", weekStart, CancellationToken.None);
        return View(model);
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

        var created = await _dataService.GenerateDoctorSchedulesAsync(model, CancellationToken.None);
        TempData["InfoMessage"] = created > 0
            ? $"Đã sinh {created} lịch làm việc mới."
            : "Không có lịch mới được tạo. Có thể lịch đã tồn tại hoặc thiếu thông tin.";

        return RedirectToAction(nameof(Index), new
        {
            mode = "calendar",
            weekStart = model.StartDate.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSchedule(string documentId, bool isActive, int availableSlots, string? roomNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            TempData["InfoMessage"] = "Không xác định được lịch cần cập nhật.";
            return RedirectToAction(nameof(Index), new { mode = "list" });
        }

        if (string.IsNullOrWhiteSpace(roomNumber))
        {
            TempData["InfoMessage"] = "Vui lòng nhập phòng khám.";
            return RedirectToAction(nameof(Index), new { mode = "list" });
        }

        if (availableSlots < 0 || availableSlots > 100)
        {
            TempData["InfoMessage"] = "Số slot khả dụng phải từ 0 đến 100.";
            return RedirectToAction(nameof(Index), new { mode = "list" });
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

        return RedirectToAction(nameof(Index), new { mode = "list" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BackfillRooms(WorkScheduleBackfillRoomViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["InfoMessage"] = string.Join(" ", ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage)
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            return RedirectToAction(nameof(Index), new { mode = "list" });
        }

        var updated = await _dataService.BackfillScheduleRoomsAsync(model, CancellationToken.None);
        TempData["InfoMessage"] = updated > 0
            ? $"Đã thêm phòng cho {updated} lịch làm việc cũ."
            : "Không có lịch cũ nào cần thêm phòng theo bộ lọc này.";

        return RedirectToAction(nameof(Index), new { mode = "list" });
    }
}
