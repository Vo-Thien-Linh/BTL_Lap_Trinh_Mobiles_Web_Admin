using System.ComponentModel.DataAnnotations;

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
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? UserStatus { get; set; }
    public bool? EmailVerified { get; set; }
}

public class DoctorCreateViewModel
{
    [Required]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "Ảnh đại diện")]
    public string? AvatarUrl { get; set; }

    [Display(Name = "Giới tính")]
    public string? Gender { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Ngày sinh")]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Trạng thái tài khoản")]
    public string UserStatus { get; set; } = "pending";

    [Display(Name = "Đã xác minh email")]
    public bool EmailVerified { get; set; }

    [Display(Name = "UID tài khoản")]
    public string? UserId { get; set; }

    [Required]
    [Display(Name = "Mã khoa")]
    public string DepartmentId { get; set; } = string.Empty;

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
