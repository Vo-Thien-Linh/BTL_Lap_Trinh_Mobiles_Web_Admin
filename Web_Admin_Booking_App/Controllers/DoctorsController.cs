using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

[Authorize(Policy = "AdminOnly")]
public class DoctorsController : Controller
{
    private readonly FirestoreAdminDataService _dataService;
    private readonly FirebaseAuthRestService _authService;
    private readonly IWebHostEnvironment _environment;

    public DoctorsController(
        FirestoreAdminDataService dataService,
        FirebaseAuthRestService authService,
        IWebHostEnvironment environment)
    {
        _dataService = dataService;
        _authService = authService;
        _environment = environment;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var doctors = await _dataService.GetDoctorsAsync(cancellationToken);
        return View(doctors);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await BuildDoctorCreateModelAsync(new DoctorCreateViewModel(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DoctorCreateViewModel model, CancellationToken cancellationToken)
    {
        NormalizeDoctorCreateModel(model);
        ValidateAvatarFile(model);

        if (!ModelState.IsValid)
        {
            return View(await BuildDoctorCreateModelAsync(model, cancellationToken));
        }

        var unique = await _dataService.CheckUserUniqueFieldsAsync(
            model.Email,
            model.Phone,
            model.Cccd,
            ignoredUserId: null,
            cancellationToken);

        if (unique.EmailExists)
        {
            ModelState.AddModelError(nameof(model.Email), "Email này đã tồn tại trong collection users.");
        }

        if (unique.PhoneExists)
        {
            ModelState.AddModelError(nameof(model.Phone), "Số điện thoại này đã tồn tại trong collection users.");
        }

        if (unique.CccdExists)
        {
            ModelState.AddModelError(nameof(model.Cccd), "CCCD này đã tồn tại trong collection users.");
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildDoctorCreateModelAsync(model, cancellationToken));
        }

        try
        {
            model.AvatarUrl = await SaveAvatarFileAsync(model.AvatarFile);
            var authUser = await _authService.CreateUserWithPasswordAsync(model.Email, model.Password);
            var doctorCode = await _dataService.CreateDoctorAsync(model, authUser.LocalId, CancellationToken.None);
            TempData["InfoMessage"] = $"Đã tạo tài khoản bác sĩ thành công. Mã bác sĩ: {doctorCode}.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(await BuildDoctorCreateModelAsync(model, cancellationToken));
        }
    }

    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var doctor = await _dataService.GetDoctorDetailsAsync(id, cancellationToken);
        if (doctor is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy bác sĩ trên Firebase.";
            return RedirectToAction(nameof(Index));
        }

        return View(doctor);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string id, CancellationToken cancellationToken)
    {
        await _dataService.UpdateDoctorVerificationAsync(id, "verified", cancellationToken: CancellationToken.None);
        TempData["InfoMessage"] = "Đã duyệt hồ sơ bác sĩ.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(string id, string? rejectReason, CancellationToken cancellationToken)
    {
        await _dataService.UpdateDoctorVerificationAsync(id, "rejected", rejectReason, CancellationToken.None);
        TempData["InfoMessage"] = "Đã từ chối hồ sơ bác sĩ.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private static void NormalizeDoctorCreateModel(DoctorCreateViewModel model)
    {
        model.FullName = model.FullName.Trim();
        model.Email = model.Email.Trim().ToLowerInvariant();
        model.Phone = DigitsOnly(model.Phone);
        model.Cccd = DigitsOnly(model.Cccd);
        model.DepartmentId = model.DepartmentId.Trim();
        model.Specialization = model.Specialization.Trim();
        model.LicenseNumber = model.LicenseNumber.Trim();
        model.UserStatus = model.UserStatus.Trim();
        model.VerificationStatus = model.VerificationStatus.Trim();
        if (!model.IsFeatured)
        {
            model.FeaturedRank = null;
        }
    }

    private static string DigitsOnly(string value)
    {
        return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    private void ValidateAvatarFile(DoctorCreateViewModel model)
    {
        if (model.AvatarFile is null || model.AvatarFile.Length == 0) return;

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        var extension = Path.GetExtension(model.AvatarFile.FileName);
        if (!allowedExtensions.Contains(extension))
        {
            ModelState.AddModelError(nameof(model.AvatarFile), "Ảnh đại diện chỉ hỗ trợ JPG, PNG hoặc WEBP.");
        }

        const long maxBytes = 2 * 1024 * 1024;
        if (model.AvatarFile.Length > maxBytes)
        {
            ModelState.AddModelError(nameof(model.AvatarFile), "Ảnh đại diện không được vượt quá 2MB.");
        }
    }

    private async Task<string?> SaveAvatarFileAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0) return null;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var relativeDirectory = Path.Combine("uploads", "avatars");
        var absoluteDirectory = Path.Combine(_environment.WebRootPath, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var absolutePath = Path.Combine(absoluteDirectory, fileName);
        await using var stream = System.IO.File.Create(absolutePath);
        await file.CopyToAsync(stream, CancellationToken.None);

        return "/" + Path.Combine(relativeDirectory, fileName).Replace("\\", "/", StringComparison.Ordinal);
    }

    private async Task<DoctorCreateViewModel> BuildDoctorCreateModelAsync(
        DoctorCreateViewModel model,
        CancellationToken cancellationToken)
    {
        model.Departments = await _dataService.GetDepartmentOptionsAsync(cancellationToken);
        return model;
    }
}
