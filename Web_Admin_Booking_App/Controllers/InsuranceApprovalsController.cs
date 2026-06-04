using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

[Authorize(Policy = "StaffOnly")]
public sealed class InsuranceApprovalsController : Controller
{
    private readonly PatientProfileResolver _patientResolver;

    public InsuranceApprovalsController(PatientProfileResolver patientResolver)
    {
        _patientResolver = patientResolver;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(search) && search.Trim().Length > 100)
        {
            var invalidModel = await _patientResolver.GetInsuranceApprovalsAsync(null, cancellationToken);
            invalidModel.Search = search;
            invalidModel.FilterError = "Từ khóa tìm kiếm không được vượt quá 100 ký tự.";
            invalidModel.Items.Clear();
            return View(invalidModel);
        }

        var model = await _patientResolver.GetInsuranceApprovalsAsync(search, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string patientId, DateOnly? expiryDate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(patientId)) return BadRequest();
        if (!expiryDate.HasValue)
        {
            TempData["ErrorMessage"] = "Vui lòng nhập ngày hết hạn BHYT.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var updated = await _patientResolver.ApproveInsuranceAsync(
                patientId,
                expiryDate.Value,
                User.Identity?.Name ?? "staff",
                cancellationToken);
            if (!updated) return NotFound();
            TempData["SuccessMessage"] = "Đã duyệt BHYT và đồng bộ hồ sơ bệnh nhân.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(string patientId, string? rejectReason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(patientId)) return BadRequest();
        if (string.IsNullOrWhiteSpace(rejectReason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do từ chối BHYT.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var updated = await _patientResolver.RejectInsuranceAsync(
                patientId,
                rejectReason,
                User.Identity?.Name ?? "staff",
                cancellationToken);
            if (!updated) return NotFound();
            TempData["SuccessMessage"] = "Đã từ chối BHYT và gửi thông báo cho bệnh nhân.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
