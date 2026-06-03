using System.ComponentModel.DataAnnotations;

namespace Web_Admin_Booking_App.Models;

public sealed class MedicineIndexViewModel
{
    public string? Search { get; set; }
    public string? GroupFilter { get; set; }
    public int TotalCount { get; set; }
    public int LowStockCount { get; set; }
    public decimal InventoryValue { get; set; }
    public IReadOnlyList<string> Groups { get; set; } = Array.Empty<string>();
    public IReadOnlyList<MedicineListItemViewModel> Items { get; set; } = Array.Empty<MedicineListItemViewModel>();
}

public sealed class MedicineListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string MedicineCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Group { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool IsActive { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class MedicineUpsertViewModel
{
    public string? Id { get; set; }
    public string? MedicineCode { get; set; }
    public string? CodePreview { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên thuốc.")]
    [StringLength(160, ErrorMessage = "Tên thuốc không được vượt quá 160 ký tự.")]
    [Display(Name = "Tên thuốc")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập hàm lượng.")]
    [StringLength(80, ErrorMessage = "Hàm lượng không được vượt quá 80 ký tự.")]
    [Display(Name = "Hàm lượng")]
    public string Strength { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập đơn vị.")]
    [StringLength(40, ErrorMessage = "Đơn vị không được vượt quá 40 ký tự.")]
    [Display(Name = "Đơn vị")]
    public string Unit { get; set; } = string.Empty;

    [Range(0, 999999999, ErrorMessage = "Đơn giá không hợp lệ.")]
    [Display(Name = "Đơn giá")]
    public decimal UnitPrice { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nhóm thuốc.")]
    [StringLength(120, ErrorMessage = "Nhóm thuốc không được vượt quá 120 ký tự.")]
    [Display(Name = "Nhóm thuốc")]
    public string Group { get; set; } = string.Empty;

    [Range(0, 999999, ErrorMessage = "Số lượng thuốc không hợp lệ.")]
    [Display(Name = "Số lượng")]
    public int Quantity { get; set; }

    [Range(0, 999999, ErrorMessage = "Ngưỡng cảnh báo không hợp lệ.")]
    [Display(Name = "Ngưỡng cảnh báo tồn kho")]
    public int LowStockThreshold { get; set; } = 10;

    [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    [Display(Name = "Đang sử dụng")]
    public bool IsActive { get; set; } = true;

    public IReadOnlyList<string> UnitSuggestions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> GroupSuggestions { get; set; } = Array.Empty<string>();
}
