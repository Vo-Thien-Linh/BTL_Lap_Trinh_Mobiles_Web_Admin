using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Web_Admin_Booking_App.Models;

public class DoctorListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Department { get; set; } = string.Empty;
    public string? DepartmentId { get; set; }
    public string? Specialization { get; set; }
    public string? LicenseNumber { get; set; }
    public string? Degree { get; set; }
    public string? YearsOfExperience { get; set; }
    public string? ConsultationFee { get; set; }
    public string? VerificationStatus { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsFeatured { get; set; }
    public int? FeaturedRank { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? UserStatus { get; set; }
    public bool? EmailVerified { get; set; }
}

public class DoctorCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại phải gồm đúng 10 chữ số.")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập CCCD.")]
    [RegularExpression(@"^\d{12}$", ErrorMessage = "CCCD phải gồm đúng 12 chữ số.")]
    [Display(Name = "CCCD")]
    public string Cccd { get; set; } = string.Empty;

    [Display(Name = "Ảnh đại diện")]
    public string? AvatarUrl { get; set; }

    [Display(Name = "Ảnh đại diện")]
    public IFormFile? AvatarFile { get; set; }

    [Display(Name = "Giới tính")]
    public string? Gender { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Ngày sinh")]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Trạng thái tài khoản")]
    public string UserStatus { get; set; } = "pending";

    [Display(Name = "Đã xác minh email")]
    public bool EmailVerified { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn khoa.")]
    [Display(Name = "Khoa")]
    public string DepartmentId { get; set; } = string.Empty;

    public IReadOnlyList<SelectOption> Departments { get; set; } = Array.Empty<SelectOption>();

    [Display(Name = "Chuyên khoa")]
    public string Specialization { get; set; } = string.Empty;

    [Display(Name = "Chứng chỉ hành nghề")]
    public string LicenseNumber { get; set; } = string.Empty;

    [Display(Name = "Học vị")]
    public string? Degree { get; set; }

    [Range(0, 80)]
    [Display(Name = "Số năm kinh nghiệm")]
    public int? YearsOfExperience { get; set; }

    [Range(0, 999999999)]
    [Display(Name = "Phí khám")]
    public decimal? ConsultationFee { get; set; }

    [Display(Name = "Giới thiệu bác sĩ")]
    public string? Biography { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Bác sĩ nổi bật")]
    public bool IsFeatured { get; set; }

    [Range(1, 999, ErrorMessage = "Thá»© háº¡ng ná»•i báº­t pháº£i tá»« 1 Ä‘áº¿n 999.")]
    [Display(Name = "Thá»© háº¡ng ná»•i báº­t")]
    public int? FeaturedRank { get; set; }

    [Display(Name = "Trạng thái duyệt hồ sơ")]
    public string VerificationStatus { get; set; } = "pending";
}

public class DoctorDetailsViewModel
{
    public string Id { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Department { get; set; } = string.Empty;
    public string? DepartmentId { get; set; }
    public string? Specialization { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Role { get; set; }
    public string? UserStatus { get; set; }
    public bool? EmailVerified { get; set; }
    public string? LicenseNumber { get; set; }
    public string? Degree { get; set; }
    public string? YearsOfExperience { get; set; }
    public string? ConsultationFee { get; set; }
    public string? Biography { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsFeatured { get; set; }
    public int? FeaturedRank { get; set; }
    public string? VerificationStatus { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<DoctorDetailFieldViewModel> ProfileFields { get; set; } = Array.Empty<DoctorDetailFieldViewModel>();
    public IReadOnlyList<DoctorDetailFieldViewModel> ContactFields { get; set; } = Array.Empty<DoctorDetailFieldViewModel>();
    public IReadOnlyList<DoctorDetailFieldViewModel> ProfessionalFields { get; set; } = Array.Empty<DoctorDetailFieldViewModel>();
    public IReadOnlyList<DoctorDetailFieldViewModel> AdminFields { get; set; } = Array.Empty<DoctorDetailFieldViewModel>();
}

public class DoctorDetailFieldViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
