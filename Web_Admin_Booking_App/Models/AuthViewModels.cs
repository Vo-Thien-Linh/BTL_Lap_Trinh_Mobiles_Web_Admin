using System.ComponentModel.DataAnnotations;

namespace Web_Admin_Booking_App.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}
