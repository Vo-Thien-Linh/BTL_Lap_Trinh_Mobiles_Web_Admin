using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

public class WorkShiftsController : Controller
{
    private readonly FirestoreAdminDataService _dataService;

    public WorkShiftsController(FirestoreAdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<IActionResult> Index(string? mode = null, CancellationToken cancellationToken = default)
    {
        var model = await _dataService.GetWorkShiftsAsync(mode, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(WorkScheduleGenerateViewModel model, CancellationToken cancellationToken)
    {
        var created = await _dataService.GenerateDoctorSchedulesAsync(model, cancellationToken);
        TempData["InfoMessage"] = created > 0
            ? $"Đã sinh {created} lịch làm việc mới."
            : "Không có lịch mới được tạo. Có thể lịch đã tồn tại hoặc thiếu thông tin.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSchedule(string documentId, bool isActive, int availableSlots, string? room, CancellationToken cancellationToken)
    {
        try
        {
            await _dataService.UpdateDoctorScheduleAsync(documentId, isActive, availableSlots, room, cancellationToken);
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
        var updated = await _dataService.BackfillScheduleRoomsAsync(model, cancellationToken);
        TempData["InfoMessage"] = updated > 0
            ? $"Đã thêm phòng cho {updated} lịch làm việc cũ."
            : "Không có lịch cũ nào cần thêm phòng theo bộ lọc này.";

        return RedirectToAction(nameof(Index), new { mode = "list" });
    }
}
