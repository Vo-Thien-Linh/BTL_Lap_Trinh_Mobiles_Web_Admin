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
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("admin"))
        {
            return RedirectToAction("Index", "Home");
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
                !string.Equals(adminUser.Role, "admin", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(adminUser.Status, "active", StringComparison.OrdinalIgnoreCase))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction(nameof(AccessDenied));
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, adminUser.Uid),
                new(ClaimTypes.Email, string.IsNullOrWhiteSpace(adminUser.Email) ? model.Email : adminUser.Email),
                new(ClaimTypes.Name, string.IsNullOrWhiteSpace(adminUser.FullName) ? model.Email : adminUser.FullName),
                new(ClaimTypes.Role, "admin"),
                new("role", "admin")
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

            return RedirectToAction("Index", "Home");
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
