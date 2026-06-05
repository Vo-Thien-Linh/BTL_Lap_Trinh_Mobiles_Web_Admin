using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

[Authorize(Policy = "AdminOrStaff")]
public class MedicalRecordsController : Controller
{
    private readonly FirestoreAdminDataService _dataService;

    public MedicalRecordsController(FirestoreAdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<IActionResult> Index(
        string? search,
        string? statusFilter,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var filterError = ValidateFilters(search, statusFilter, fromDate, toDate);
        if (filterError != null)
        {
            return View(new MedicalRecordsIndexViewModel
            {
                Search = search,
                StatusFilter = statusFilter,
                FromDate = fromDate,
                ToDate = toDate,
                Page = 1,
                PageSize = 10,
                TotalCount = 0,
                FilterError = filterError
            });
        }

        var model = await _dataService.GetMedicalRecordsAsync(search, statusFilter, fromDate, toDate, page, 10, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var model = await _dataService.GetMedicalRecordDetailsAsync(id, cancellationToken);
        if (model == null) return NotFound();

        return View(model);
    }

    private static string? ValidateFilters(string? search, string? statusFilter, DateOnly? fromDate, DateOnly? toDate)
    {
        if (!string.IsNullOrWhiteSpace(search) && search.Trim().Length > 100)
        {
            return "Từ khóa tìm kiếm không được vượt quá 100 ký tự.";
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && !Enum.TryParse<MedicalRecordStatus>(statusFilter, true, out _))
        {
            return "Trạng thái bệnh án không hợp lệ.";
        }

        if (fromDate.HasValue && toDate.HasValue && toDate.Value < fromDate.Value)
        {
            return "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.";
        }

        return null;
    }
}
