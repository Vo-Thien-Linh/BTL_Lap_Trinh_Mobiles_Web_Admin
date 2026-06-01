using System.ComponentModel.DataAnnotations;

namespace Web_Admin_Booking_App.Models;

public enum Gender
{
    Unknown = 0,
    Male = 1,
    Female = 2,
    Other = 3
}

public enum PatientStatus
{
    Active = 1,
    Inactive = 2,
    Blocked = 3
}

public sealed class PatientIndexViewModel
{
    public string? Search { get; set; }
    public string? GenderFilter { get; set; }
    public string? StatusFilter { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int MissingInsuranceCount { get; set; }
    public int HasInsuranceCount => Math.Max(0, TotalCount - MissingInsuranceCount);
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize)));
    public List<PatientListItemViewModel> Items { get; set; } = new();
}

public sealed class PatientListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = "Chưa có tên";
    public string Phone { get; set; } = "Chưa có số điện thoại";
    public string Email { get; set; } = string.Empty;
    public Gender Gender { get; set; } = Gender.Unknown;
    public DateOnly? Dob { get; set; }
    public PatientStatus Status { get; set; } = PatientStatus.Active;
    public string HealthInsuranceNumber { get; set; } = string.Empty;
    public string HealthInsuranceStatus { get; set; } = string.Empty;

    public string AvatarText
    {
        get
        {
            var normalized = string.IsNullOrWhiteSpace(FullName) ? "BN" : FullName.Trim();
            var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "BN";
            if (parts.Length == 1) return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
            return string.Concat(parts.TakeLast(2).Select(p => p[0])).ToUpperInvariant();
        }
    }
}

public sealed class PatientDetailsViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = "Chưa có tên";
    public string Username { get; set; } = string.Empty;
    public string Phone { get; set; } = "Chưa có số điện thoại";
    public string Email { get; set; } = string.Empty;
    public string Cccd { get; set; } = string.Empty;
    public Gender Gender { get; set; } = Gender.Unknown;
    public DateOnly? Dob { get; set; }
    public string Address { get; set; } = "Chưa cập nhật";
    public string BloodType { get; set; } = string.Empty;
    public string Allergy { get; set; } = string.Empty;
    public string ChronicDisease { get; set; } = string.Empty;
    public string HealthInsuranceNumber { get; set; } = string.Empty;
    public string HealthInsuranceStatus { get; set; } = string.Empty;
    public PatientStatus Status { get; set; } = PatientStatus.Active;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastVisitAt { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<PatientAppointmentSummaryViewModel> RecentAppointments { get; set; } = new();

    public string AvatarText
    {
        get
        {
            var normalized = string.IsNullOrWhiteSpace(FullName) ? "BN" : FullName.Trim();
            var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "BN";
            if (parts.Length == 1) return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
            return string.Concat(parts.TakeLast(2).Select(p => p[0])).ToUpperInvariant();
        }
    }
}

public sealed class PatientAppointmentSummaryViewModel
{
    public string Id { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public DateTime? AppointmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Symptoms { get; set; } = string.Empty;
}
