using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

[Authorize(Policy = "AdminOnly")]
public sealed class StaffController : Controller
{
    private readonly FirestoreAdminDataService _dataService;
    private readonly FirebaseAuthRestService _authService;

    public StaffController(FirestoreAdminDataService dataService, FirebaseAuthRestService authService)
    {
        _dataService = dataService;
        _authService = authService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new StaffIndexViewModel
        {
            Items = await _dataService.GetStaffAsync(cancellationToken)
        };
        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new StaffCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffCreateViewModel model, CancellationToken cancellationToken)
    {
        Normalize(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var unique = await _dataService.CheckUserUniqueFieldsAsync(
            model.Email,
            model.Phone,
            model.Cccd ?? string.Empty,
            ignoredUserId: null,
            cancellationToken);

        if (unique.EmailExists)
        {
            ModelState.AddModelError(nameof(model.Email), "Email này đã tồn tại trong users.");
        }

        if (unique.PhoneExists)
        {
            ModelState.AddModelError(nameof(model.Phone), "Số điện thoại này đã tồn tại trong users.");
        }

        if (unique.CccdExists)
        {
            ModelState.AddModelError(nameof(model.Cccd), "CCCD này đã tồn tại trong users.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var authUser = await _authService.CreateUserWithPasswordAsync(model.Email, model.Password);
            var staffCode = await _dataService.CreateStaffAsync(model, authUser.LocalId, CancellationToken.None);
            TempData["SuccessMessage"] = $"Đã tạo tài khoản nhân viên {staffCode}.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    private static void Normalize(StaffCreateViewModel model)
    {
        model.FullName = model.FullName.Trim();
        model.Email = model.Email.Trim().ToLowerInvariant();
        model.Phone = DigitsOnly(model.Phone);
        model.Cccd = DigitsOnly(model.Cccd);
        model.Role = "staff";
        model.StaffType = "general";
        model.Position = string.IsNullOrWhiteSpace(model.Position) ? "Nhân viên quầy" : model.Position.Trim();
        model.Status = string.IsNullOrWhiteSpace(model.Status) ? "active" : model.Status.Trim().ToLowerInvariant();
    }

    private static string DigitsOnly(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsDigit).ToArray());
    }
}
