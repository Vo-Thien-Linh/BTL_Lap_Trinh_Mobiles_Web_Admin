using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

[Authorize(Policy = "AdminOrStaff")]
public sealed class PatientsController : Controller
{
    private readonly PatientProfileResolver _patientResolver;

    public PatientsController(PatientProfileResolver patientResolver)
    {
        _patientResolver = patientResolver;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? genderFilter,
        string? statusFilter,
        string? insuranceFilter,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var allPatients = (await _patientResolver.GetPatientsAsync(cancellationToken)).ToList();
        var filterError = ValidateFilters(search, genderFilter, statusFilter, insuranceFilter);
        var filtered = filterError == null
            ? ApplyPatientFilters(allPatients, search, genderFilter, statusFilter, insuranceFilter).ToList()
            : new List<PatientListItemViewModel>();

        var pageSize = 10;
        var safePage = Math.Max(1, page);
        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
        safePage = Math.Min(safePage, totalPages);

        var model = new PatientIndexViewModel
        {
            Search = search,
            GenderFilter = genderFilter,
            StatusFilter = statusFilter,
            InsuranceFilter = insuranceFilter,
            FilterError = filterError,
            Page = safePage,
            PageSize = pageSize,
            TotalCount = filtered.Count,
            ActiveCount = allPatients.Count(x => x.Status == PatientStatus.Active),
            MissingInsuranceCount = allPatients.Count(x => string.Equals(x.HealthInsuranceStatus, "none", StringComparison.OrdinalIgnoreCase)),
            PendingInsuranceCount = allPatients.Count(x => string.Equals(x.HealthInsuranceStatus, "pending", StringComparison.OrdinalIgnoreCase)),
            ApprovedInsuranceCount = allPatients.Count(x => string.Equals(x.HealthInsuranceStatus, "approved", StringComparison.OrdinalIgnoreCase)),
            RejectedInsuranceCount = allPatients.Count(x => string.Equals(x.HealthInsuranceStatus, "rejected", StringComparison.OrdinalIgnoreCase)),
            ExpiredInsuranceCount = allPatients.Count(x => string.Equals(x.HealthInsuranceStatus, "expired", StringComparison.OrdinalIgnoreCase)),
            Items = filtered
                .Skip((safePage - 1) * pageSize)
                .Take(pageSize)
                .ToList()
        };

        if (IsAjaxRequest())
        {
            return PartialView("_PatientTable", model);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var patient = await _patientResolver.GetPatientDetailsAsync(id, cancellationToken);
        if (patient == null) return NotFound();

        return View(patient);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> UpdateInsuranceStatus(
        string id,
        string status,
        DateOnly? expiryDate,
        string? rejectReason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest();

        status = status?.Trim().ToLowerInvariant() ?? string.Empty;
        if (status is not ("approved" or "rejected")) return BadRequest();

        try
        {
            var updated = status == "approved"
                ? await _patientResolver.ApproveInsuranceAsync(
                    id,
                    expiryDate ?? throw new InvalidOperationException("Vui lòng nhập ngày hết hạn BHYT."),
                    User.Identity?.Name ?? "staff",
                    cancellationToken)
                : await _patientResolver.RejectInsuranceAsync(
                    id,
                    rejectReason ?? string.Empty,
                    User.Identity?.Name ?? "staff",
                    cancellationToken);

            if (!updated) return NotFound();
            TempData["SuccessMessage"] = status == "approved" ? "Đã duyệt BHYT." : "Đã từ chối BHYT.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction("Index", "InsuranceApprovals");
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<PatientListItemViewModel> ApplyPatientFilters(
        IEnumerable<PatientListItemViewModel> source,
        string? search,
        string? genderFilter,
        string? statusFilter,
        string? insuranceFilter)
    {
        var query = source;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.FullName.ToLowerInvariant().Contains(key) ||
                x.Phone.ToLowerInvariant().Contains(key) ||
                x.Email.ToLowerInvariant().Contains(key) ||
                x.Cccd.ToLowerInvariant().Contains(key) ||
                x.Code.ToLowerInvariant().Contains(key) ||
                x.Id.ToLowerInvariant().Contains(key) ||
                x.HealthInsuranceNumber.ToLowerInvariant().Contains(key) ||
                x.PendingHealthInsuranceNumber.ToLowerInvariant().Contains(key));
        }

        if (!string.IsNullOrWhiteSpace(genderFilter) && Enum.TryParse<Gender>(genderFilter, true, out var gender))
        {
            query = query.Where(x => x.Gender == gender);
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<PatientStatus>(statusFilter, true, out var status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(insuranceFilter))
        {
            query = insuranceFilter.Equals("missing", StringComparison.OrdinalIgnoreCase)
                ? query.Where(x => string.Equals(x.HealthInsuranceStatus, "none", StringComparison.OrdinalIgnoreCase))
                : query.Where(x => string.Equals(x.HealthInsuranceStatus, insuranceFilter, StringComparison.OrdinalIgnoreCase));
        }

        return query;
    }

    private static string? ValidateFilters(string? search, string? genderFilter, string? statusFilter, string? insuranceFilter)
    {
        if (!string.IsNullOrWhiteSpace(search) && search.Trim().Length > 100)
        {
            return "Từ khóa tìm kiếm không được vượt quá 100 ký tự.";
        }

        if (!string.IsNullOrWhiteSpace(genderFilter) && !Enum.TryParse<Gender>(genderFilter, true, out _))
        {
            return "Bộ lọc giới tính không hợp lệ.";
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && !Enum.TryParse<PatientStatus>(statusFilter, true, out _))
        {
            return "Bộ lọc trạng thái tài khoản không hợp lệ.";
        }

        var allowedInsurance = new[] { "pending", "approved", "rejected", "expired", "missing" };
        if (!string.IsNullOrWhiteSpace(insuranceFilter) &&
            !allowedInsurance.Contains(insuranceFilter, StringComparer.OrdinalIgnoreCase))
        {
            return "Bộ lọc trạng thái BHYT không hợp lệ.";
        }

        return null;
    }
}
