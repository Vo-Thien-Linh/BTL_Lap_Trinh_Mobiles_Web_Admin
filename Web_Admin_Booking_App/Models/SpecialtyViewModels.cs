using System.ComponentModel.DataAnnotations;

namespace Web_Admin_Booking_App.Models;

public enum SpecialtyStatus
{
    Active = 1,
    Paused = 2
}

public enum SpecialtyIcon
{
    General = 0,
    Heart = 1,
    Brain = 2,
    Baby = 3,
    Lab = 4,
    Skin = 5,
    Eye = 6,
    Bone = 7,
    Tooth = 8
}

public sealed class SpecialtyIndexViewModel
{
    public string? Search { get; set; }
    public string? StatusFilter { get; set; }
    public string? FilterError { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 8;
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int NewThisMonthCount { get; set; }
    public string NewThisMonthNames { get; set; } = string.Empty;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize)));
    public List<SpecialtyListItemViewModel> Items { get; set; } = new();
}

public sealed class SpecialtyListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LocationNote { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int RoomCount { get; set; }
    public string Code { get; set; } = string.Empty;
    public string HeadDoctor { get; set; } = string.Empty;
    public int DoctorCount { get; set; }
    public SpecialtyStatus Status { get; set; } = SpecialtyStatus.Active;
    public SpecialtyIcon Icon { get; set; } = SpecialtyIcon.General;
    public DateTime? CreatedAt { get; set; }
}

public sealed class SpecialtyUpsertViewModel
{
    public string Id { get; set; } = string.Empty;
    public string CodePreview { get; set; } = string.Empty;

    [Display(Name = "Tên chuyên khoa")]
    [Required(ErrorMessage = "Vui lòng nhập tên chuyên khoa")]
    [StringLength(120, ErrorMessage = "Tên chuyên khoa không được vượt quá 120 ký tự")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Mã khoa")]
    [StringLength(40, ErrorMessage = "Mã khoa không được vượt quá 40 ký tự")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Trưởng khoa")]
    [StringLength(120, ErrorMessage = "Tên trưởng khoa không được vượt quá 120 ký tự")]
    public string HeadDoctor { get; set; } = string.Empty;

    [Display(Name = "Số bác sĩ")]
    [Range(0, 999, ErrorMessage = "Số bác sĩ không hợp lệ")]
    public int DoctorCount { get; set; }

    [Display(Name = "Vị trí")]
    [StringLength(160, ErrorMessage = "Vị trí không được vượt quá 160 ký tự")]
    public string LocationNote { get; set; } = string.Empty;

    [Display(Name = "Mô tả")]
    [StringLength(600, ErrorMessage = "Mô tả không được vượt quá 600 ký tự")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Số điện thoại khoa")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại phải gồm đúng 10 chữ số")]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "Ảnh khoa")]
    [StringLength(500, ErrorMessage = "Đường dẫn ảnh không được vượt quá 500 ký tự")]
    public string ImageUrl { get; set; } = string.Empty;

    [Display(Name = "Logo khoa")]
    public IFormFile? LogoFile { get; set; }

    [Display(Name = "Phòng khám")]
    public string RoomsText { get; set; } = string.Empty;

    public List<string> RoomNumbers { get; set; } = new();

    [Display(Name = "Trạng thái")]
    public SpecialtyStatus Status { get; set; } = SpecialtyStatus.Active;

    [Display(Name = "Biểu tượng")]
    public SpecialtyIcon Icon { get; set; } = SpecialtyIcon.General;

    public List<string> ExistingNames { get; set; } = new();
    public List<string> ExistingCodes { get; set; } = new();
    public List<string> DoctorSuggestions { get; set; } = new();
    public List<string> LocationSuggestions { get; set; } = new();
}
