using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

public class AuthController : Controller
{
    private readonly FirebaseAuthRestService _authService;
    private readonly FirebaseAdminUserService _adminUserService;

    public AuthController(FirebaseAuthRestService authService, FirebaseAdminUserService adminUserService)
    {
        _authService = authService;
        _adminUserService = adminUserService;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleHome();
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var authResult = await _authService.SignInWithPasswordAsync(model.Email, model.Password);
            var adminUser = await _adminUserService.GetAdminUserAsync(authResult.LocalId);

            if (adminUser == null ||
                !IsAllowedWebRole(adminUser.Role) ||
                !string.Equals(adminUser.Status, "active", StringComparison.OrdinalIgnoreCase))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction(nameof(AccessDenied));
            }

            var normalizedRole = NormalizeRole(adminUser.Role);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, adminUser.Uid),
                new(ClaimTypes.Email, string.IsNullOrWhiteSpace(adminUser.Email) ? model.Email : adminUser.Email),
                new(ClaimTypes.Name, string.IsNullOrWhiteSpace(adminUser.FullName) ? model.Email : adminUser.FullName),
                new(ClaimTypes.Role, normalizedRole),
                new("role", normalizedRole)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToRoleHome(normalizedRole);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _authService.SendPasswordResetEmailAsync(model.Email);
            TempData["SuccessMessage"] = "Gửi email đặt lại mật khẩu thành công. Vui lòng kiểm tra hộp thư hoặc mục Spam.";
            return RedirectToAction(nameof(ForgotPassword));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToRoleHome(string? role = null)
    {
        role ??= User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return NormalizeRole(role) switch
        {
            "staff" => RedirectToAction("StaffDashboard", "Home"),
            "admin" => RedirectToAction("Index", "Home"),
            _ => RedirectToAction(nameof(AccessDenied))
        };
    }

    private static bool IsAllowedWebRole(string? role)
    {
        return NormalizeRole(role) is "admin" or "staff";
    }

    private static string NormalizeRole(string? role)
    {
        return role?.Trim().ToLowerInvariant() switch
        {
            "cashier" or "receptionist" or "staff_manager" => "staff",
            "admin" => "admin",
            "staff" => "staff",
            "doctor" => "doctor",
            "patient" => "patient",
            _ => string.Empty
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
