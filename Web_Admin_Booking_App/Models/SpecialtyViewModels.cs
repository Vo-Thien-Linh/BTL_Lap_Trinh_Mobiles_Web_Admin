using System.ComponentModel.DataAnnotations;

namespace Web_Admin_Booking_App.Models;

public enum SpecialtyStatus
{
    Active = 1,
    Paused = 2,
}

public enum SpecialtyIcon
{
    Heart = 1,
    Brain = 2,
    Baby = 3,
    Lab = 4,
    Default = 99,
}

public class SpecialtyListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LocationNote { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string HeadDoctor { get; set; } = string.Empty;
    public int DoctorCount { get; set; }
    public SpecialtyStatus Status { get; set; }
    public SpecialtyIcon Icon { get; set; }
}

public class SpecialtyIndexViewModel
{
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int NewThisMonthCount { get; set; }
    public string NewThisMonthNames { get; set; } = string.Empty;

    public IReadOnlyList<SpecialtyListItemViewModel> Items { get; set; } = Array.Empty<SpecialtyListItemViewModel>();
}

public class SpecialtyUpsertViewModel
{
    public int? Id { get; set; }

    [Required]
    [Display(Name = "Tên chuyên khoa")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Vị trí")]
    public string? LocationNote { get; set; }

    [Required]
    [Display(Name = "Mã khoa")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Trưởng khoa")]
    public string? HeadDoctor { get; set; }

    [Range(0, 9999)]
    [Display(Name = "Số bác sĩ")]
    public int DoctorCount { get; set; }

    [Display(Name = "Trạng thái")]
    public SpecialtyStatus Status { get; set; } = SpecialtyStatus.Active;

    [Display(Name = "Biểu tượng")]
    public SpecialtyIcon Icon { get; set; } = SpecialtyIcon.Default;
}
