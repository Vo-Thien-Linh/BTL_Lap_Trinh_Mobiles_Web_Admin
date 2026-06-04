using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

[Authorize(Policy = "StaffOnly")]
public sealed class CancelRequestsController : Controller
{
    private readonly FirestoreAdminDataService _dataService;

    public CancelRequestsController(FirestoreAdminDataService dataService)
    {
        _dataService = dataService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken = default)
    {
        var items = (await _dataService.GetAppointmentsAsync(cancellationToken))
            .Where(x => x.Status == AppointmentStatus.CancelRequested)
            .ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim().ToLowerInvariant();
            items = items.Where(x =>
                x.AppointmentCode.ToLowerInvariant().Contains(key) ||
                x.PatientCode.ToLowerInvariant().Contains(key) ||
                x.PatientName.ToLowerInvariant().Contains(key) ||
                x.DoctorName.ToLowerInvariant().Contains(key) ||
                x.CancelReason.ToLowerInvariant().Contains(key))
                .ToList();
        }

        var today = DateTime.Today;
        var model = new AppointmentIndexViewModel
        {
            Search = search,
            TotalCount = items.Count,
            TodayCount = items.Count(x => x.CancelRequestedAt?.Date == today),
            PendingCount = items.Count,
            ActiveCount = 0,
            Items = items
                .OrderByDescending(x => x.CancelRequestedAt ?? x.UpdatedAt ?? x.ScheduledAt)
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest();

        try
        {
            await _dataService.ApproveAppointmentCancelRequestAsync(
                id,
                User.Identity?.Name ?? "staff",
                CancellationToken.None);
            TempData["SuccessMessage"] = "Đã duyệt hủy lịch và trả lại slot cho ca làm việc.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(string id, string? rejectReason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest();
        if (string.IsNullOrWhiteSpace(rejectReason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do từ chối hủy lịch.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _dataService.RejectAppointmentCancelRequestAsync(
                id,
                User.Identity?.Name ?? "staff",
                rejectReason,
                CancellationToken.None);
            TempData["SuccessMessage"] = "Đã từ chối yêu cầu hủy lịch.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
