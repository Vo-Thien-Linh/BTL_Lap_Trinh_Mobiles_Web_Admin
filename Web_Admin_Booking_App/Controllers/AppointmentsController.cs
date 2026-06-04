using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Grpc.Core;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

[Authorize(Policy = "AdminOrStaff")]
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
        IReadOnlyList<AppointmentListItemViewModel> items;
        WorkShiftsIndexViewModel? scheduleModel = null;
        try
        {
            items = await _dataService.GetAppointmentsAsync(cancellationToken);
            scheduleModel = await _dataService.GetWorkShiftsAsync("calendar", null, cancellationToken);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.ResourceExhausted)
        {
            TempData["ErrorMessage"] = "Firestore đang vượt quota đọc dữ liệu. Vui lòng thử lại sau.";
            items = Array.Empty<AppointmentListItemViewModel>();
        }

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

        ViewData["WeeklySchedule"] = scheduleModel?.WeeklyTable ?? new WeeklyScheduleTableViewModel();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DoctorSchedulePartial(int weekOffset = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var weekStart = today.AddDays(-DayOfWeekToMondayBased(today.DayOfWeek)).AddDays(weekOffset * 7);
            var model = await _dataService.GetWorkShiftsAsync("calendar", weekStart, cancellationToken);
            return PartialView("_DoctorSchedulePartial", model.WeeklyTable);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.ResourceExhausted)
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return Content("Firestore đang vượt quota đọc dữ liệu. Vui lòng thử lại sau.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "StaffOnly")]
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
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> RejectCancel(string id, string? rejectReason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest();

        try
        {
            await _dataService.RejectAppointmentCancelRequestAsync(
                id,
                User.Identity?.Name ?? "admin",
                rejectReason,
                CancellationToken.None);
            TempData["InfoMessage"] = "Đã từ chối yêu cầu hủy lịch.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { statusFilter = nameof(AppointmentStatus.CancelRequested) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "StaffOnly")]
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
    [Authorize(Policy = "StaffOnly")]
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
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Create(AppointmentCreateViewModel model, CancellationToken cancellationToken)
    {
        if (model.Date < DateOnly.FromDateTime(DateTime.Today))
        {
            ModelState.AddModelError(nameof(model.Date), "Không được chọn ngày khám trong quá khứ.");
        }

        if (!ModelState.IsValid)
        {
            model = await _dataService.BuildAppointmentCreateModelAsync(model, cancellationToken);
            return View(model);
        }

        await _dataService.CreateAppointmentAsync(model, cancellationToken);
        TempData["SuccessMessage"] = "Đã lưu lịch hẹn mới.";
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
                x.PatientCode.ToLowerInvariant().Contains(key) ||
                x.DoctorCode.ToLowerInvariant().Contains(key) ||
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

    private static int DayOfWeekToMondayBased(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => 0,
            DayOfWeek.Tuesday => 1,
            DayOfWeek.Wednesday => 2,
            DayOfWeek.Thursday => 3,
            DayOfWeek.Friday => 4,
            DayOfWeek.Saturday => 5,
            DayOfWeek.Sunday => 6,
            _ => 0
        };
    }
}
