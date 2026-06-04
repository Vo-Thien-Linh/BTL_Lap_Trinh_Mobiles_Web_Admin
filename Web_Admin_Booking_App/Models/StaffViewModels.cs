using System.ComponentModel.DataAnnotations;

namespace Web_Admin_Booking_App.Models;

public sealed class StaffIndexViewModel
{
    public IReadOnlyList<StaffListItemViewModel> Items { get; set; } = Array.Empty<StaffListItemViewModel>();
}

public sealed class StaffListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string StaffCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string StaffType { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class StaffCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email đăng nhập.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [Display(Name = "Email đăng nhập")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu tạm.")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu tạm")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại phải gồm đúng 10 chữ số.")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = string.Empty;

    [RegularExpression(@"^\d{12}$", ErrorMessage = "CCCD phải gồm đúng 12 chữ số.")]
    [Display(Name = "CCCD")]
    public string? Cccd { get; set; }

    public string Role { get; set; } = "staff";

    public string? StaffType { get; set; }

    [Display(Name = "Vị trí công việc")]
    public string Position { get; set; } = "Nhân viên quầy";

    [Required]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "active";
}

public sealed class StaffDashboardViewModel
{
    public int TodayAppointmentCount { get; set; }
    public int WaitingCheckInCount { get; set; }
    public int CancelRequestCount { get; set; }
    public int PendingInsuranceCount { get; set; }
    public int CashPaymentCount { get; set; }
    public int UnpaidInvoiceCount { get; set; }
    public IReadOnlyList<DashboardAppointmentViewModel> TodayAppointments { get; set; } = Array.Empty<DashboardAppointmentViewModel>();
}
