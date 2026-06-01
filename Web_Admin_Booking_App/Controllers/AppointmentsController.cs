using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

public class AppointmentsController : Controller
{
    private readonly FirestoreAdminDataService _dataService;

    public AppointmentsController(FirestoreAdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<IActionResult> Index(string? statusFilter, CancellationToken cancellationToken)
    {
        var items = await _dataService.GetAppointmentsAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            Enum.TryParse<AppointmentStatus>(statusFilter, true, out var status))
        {
            items = items.Where(x => x.Status == status).ToList();
        }

        ViewData["StatusFilter"] = statusFilter;
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCancel(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest();

        try
        {
            await _dataService.ApproveAppointmentCancelRequestAsync(
                id,
                User.Identity?.Name ?? "admin",
                CancellationToken.None);
            TempData["InfoMessage"] = "Đã duyệt hủy lịch và trả lại slot cho ca làm việc.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { statusFilter = nameof(AppointmentStatus.CancelRequested) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkCompleted(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest();

        try
        {
            await _dataService.MarkAppointmentCompletedAsync(
                id,
                User.Identity?.Name ?? "admin",
                CancellationToken.None);
            TempData["InfoMessage"] = "Đã chuyển lịch hẹn sang trạng thái đã khám.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Create()
    {
        var vm = new AppointmentCreateViewModel
        {
            Date = DateOnly.FromDateTime(DateTime.Today),
            Time = new TimeOnly(9, 0),
            Status = AppointmentStatus.Pending,
            Doctors = Array.Empty<SelectOption>(),
            Specialties = Array.Empty<SelectOption>()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(AppointmentCreateViewModel model)
    {
        // Phần tạo lịch từ web admin nên làm sau khi thống nhất schema Firestore với app Flutter.
        // Hiện tại ưu tiên đọc dữ liệu thật từ Firebase và bảo vệ đăng nhập admin.
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        TempData["InfoMessage"] = "Chức năng tạo lịch từ Web Admin sẽ được nối Firestore ở bước tiếp theo.";
        return RedirectToAction(nameof(Index));
    }
}
