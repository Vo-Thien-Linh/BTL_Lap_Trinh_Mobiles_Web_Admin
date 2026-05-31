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

    public async Task<IActionResult> Index(
        string? search,
        string? statusFilter,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? selectedId,
        CancellationToken cancellationToken)
    {
        var items = await _dataService.GetAppointmentsAsync(cancellationToken);
        var filterError = ValidateFilters(search, statusFilter, fromDate, toDate);
        var filtered = filterError == null
            ? ApplyFilters(items, search, statusFilter, fromDate, toDate).ToList()
            : new List<AppointmentListItemViewModel>();

        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            filtered = filtered
                .OrderByDescending(x => IsSelectedAppointment(x, selectedId))
                .ThenByDescending(x => x.ScheduledAt)
                .ToList();
        }
        else
        {
            filtered = filtered.OrderByDescending(x => x.ScheduledAt).ToList();
        }

        var today = DateTime.Today;
        var model = new AppointmentIndexViewModel
        {
            Search = search,
            StatusFilter = statusFilter,
            FromDate = fromDate,
            ToDate = toDate,
            SelectedId = selectedId,
            FilterError = filterError,
            TotalCount = items.Count,
            TodayCount = items.Count(x => x.ScheduledAt.Date == today),
            PendingCount = items.Count(x => x.Status == AppointmentStatus.Pending),
            ActiveCount = items.Count(x => x.Status is AppointmentStatus.Confirmed or AppointmentStatus.Upcoming or AppointmentStatus.InProgress),
            Items = filtered
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var vm = new AppointmentCreateViewModel
        {
            Date = DateOnly.FromDateTime(DateTime.Today),
            Time = new TimeOnly(9, 0),
            Status = AppointmentStatus.Pending
        };

        return View(await _dataService.BuildAppointmentCreateModelAsync(vm, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppointmentCreateViewModel model, CancellationToken cancellationToken)
    {
        if (model.Date < DateOnly.FromDateTime(DateTime.Today))
        {
            ModelState.AddModelError(nameof(model.Date), "Khong duoc chon ngay kham trong qua khu.");
        }

        if (!ModelState.IsValid)
        {
            model = await _dataService.BuildAppointmentCreateModelAsync(model, cancellationToken);
            return View(model);
        }

        await _dataService.CreateAppointmentAsync(model, cancellationToken);
        TempData["SuccessMessage"] = "Da luu lich hen moi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> DoctorsBySpecialty(string specialtyId, CancellationToken cancellationToken)
    {
        var doctors = await _dataService.GetDoctorOptionsByDepartmentAsync(specialtyId, cancellationToken);
        return Json(doctors);
    }

    private static IEnumerable<AppointmentListItemViewModel> ApplyFilters(
        IEnumerable<AppointmentListItemViewModel> source,
        string? search,
        string? statusFilter,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        var query = source;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.AppointmentCode.ToLowerInvariant().Contains(key) ||
                x.Id.ToLowerInvariant().Contains(key) ||
                x.DocumentId.ToLowerInvariant().Contains(key) ||
                x.PatientName.ToLowerInvariant().Contains(key) ||
                x.PatientPhone.ToLowerInvariant().Contains(key) ||
                x.PatientEmail.ToLowerInvariant().Contains(key) ||
                x.DoctorName.ToLowerInvariant().Contains(key) ||
                x.SpecialtyName.ToLowerInvariant().Contains(key) ||
                x.PatientNote.ToLowerInvariant().Contains(key));
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<AppointmentStatus>(statusFilter, true, out var status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.ScheduledAt.Date >= fromDate.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.ScheduledAt.Date <= toDate.Value.ToDateTime(TimeOnly.MinValue));
        }

        return query;
    }

    private static string? ValidateFilters(string? search, string? statusFilter, DateOnly? fromDate, DateOnly? toDate)
    {
        if (!string.IsNullOrWhiteSpace(search) && search.Trim().Length > 100)
        {
            return "Từ khóa tìm kiếm không được vượt quá 100 ký tự.";
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && !Enum.TryParse<AppointmentStatus>(statusFilter, true, out _))
        {
            return "Trạng thái lịch hẹn không hợp lệ.";
        }

        if (fromDate.HasValue && toDate.HasValue && toDate.Value < fromDate.Value)
        {
            return "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.";
        }

        return null;
    }

    private static bool IsSelectedAppointment(AppointmentListItemViewModel item, string selectedId)
    {
        return string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.DocumentId, selectedId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.AppointmentCode, selectedId, StringComparison.OrdinalIgnoreCase);
    }
}
