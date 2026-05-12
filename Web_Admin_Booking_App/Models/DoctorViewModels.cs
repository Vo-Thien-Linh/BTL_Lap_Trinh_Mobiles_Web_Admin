using System.ComponentModel.DataAnnotations;

namespace Web_Admin_Booking_App.Models;

public class DoctorListItemViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class DoctorCreateViewModel
{
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Chuyên khoa")]
    public string Department { get; set; } = string.Empty;

    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "Email")]
    [EmailAddress]
    public string? Email { get; set; }

    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Đang trực";
}

public class DoctorDetailsViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Status { get; set; } = string.Empty;
}
