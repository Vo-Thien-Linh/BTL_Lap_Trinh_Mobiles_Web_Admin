using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Web_Admin_Booking_App.Models;

public class DoctorListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string DoctorCode { get; set; } = string.Empty;
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
    [Required(ErrorMessage = "Vui lÃ²ng nháº­p há» vÃ  tÃªn.")]
    [Display(Name = "Há» vÃ  tÃªn")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lÃ²ng nháº­p email.")]
    [EmailAddress(ErrorMessage = "Email khÃ´ng Ä‘Ãºng Ä‘á»‹nh dáº¡ng.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lÃ²ng nháº­p máº­t kháº©u.")]
    [MinLength(6, ErrorMessage = "Máº­t kháº©u pháº£i cÃ³ Ã­t nháº¥t 6 kÃ½ tá»±.")]
    [DataType(DataType.Password)]
    [Display(Name = "Máº­t kháº©u")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lÃ²ng nháº­p sá»‘ Ä‘iá»‡n thoáº¡i.")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Sá»‘ Ä‘iá»‡n thoáº¡i pháº£i gá»“m Ä‘Ãºng 10 chá»¯ sá»‘.")]
    [Display(Name = "Sá»‘ Ä‘iá»‡n thoáº¡i")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lÃ²ng nháº­p CCCD.")]
    [RegularExpression(@"^\d{12}$", ErrorMessage = "CCCD pháº£i gá»“m Ä‘Ãºng 12 chá»¯ sá»‘.")]
    [Display(Name = "CCCD")]
    public string Cccd { get; set; } = string.Empty;

    [Display(Name = "áº¢nh Ä‘áº¡i diá»‡n")]
    public string? AvatarUrl { get; set; }

    [Display(Name = "áº¢nh Ä‘áº¡i diá»‡n")]
    public IFormFile? AvatarFile { get; set; }

    [Display(Name = "Giá»›i tÃ­nh")]
    public string? Gender { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "NgÃ y sinh")]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Tráº¡ng thÃ¡i tÃ i khoáº£n")]
    public string UserStatus { get; set; } = "pending";

    [Display(Name = "ÄÃ£ xÃ¡c minh email")]
    public bool EmailVerified { get; set; }

    [Required(ErrorMessage = "Vui lÃ²ng chá»n khoa.")]
    [Display(Name = "Khoa")]
    public string DepartmentId { get; set; } = string.Empty;

    public IReadOnlyList<SelectOption> Departments { get; set; } = Array.Empty<SelectOption>();

    [Display(Name = "ChuyÃªn khoa")]
    public string Specialization { get; set; } = string.Empty;

    [Display(Name = "Chá»©ng chá»‰ hÃ nh nghá»")]
    public string LicenseNumber { get; set; } = string.Empty;

    [Display(Name = "Há»c vá»‹")]
    public string? Degree { get; set; }

    [Range(0, 80)]
    [Display(Name = "Sá»‘ nÄƒm kinh nghiá»‡m")]
    public int? YearsOfExperience { get; set; }

    [Range(0, 999999999)]
    [Display(Name = "PhÃ­ khÃ¡m")]
    public decimal? ConsultationFee { get; set; }

    [Display(Name = "Giá»›i thiá»‡u bÃ¡c sÄ©")]
    public string? Biography { get; set; }

    [Display(Name = "Äang hoáº¡t Ä‘á»™ng")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Bác sĩ nổi bật")]
    public bool IsFeatured { get; set; }

    [Range(1, 999, ErrorMessage = "Thứ hạng nổi bật phải từ 1 đến 999.")]
    [Display(Name = "Thứ hạng nổi bật")]
    public int? FeaturedRank { get; set; }

    [Display(Name = "Tráº¡ng thÃ¡i duyá»‡t há»“ sÆ¡")]
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
