using System.ComponentModel.DataAnnotations;

namespace Web_Admin_Booking_App.Models;

public enum AppointmentStatus
{
    Pending = 1,
    Confirmed = 2,
    Upcoming = 3,
    InProgress = 4,
    Completed = 5,
    Cancelled = 6,
}

public class AppointmentListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string AppointmentCode { get; set; } = string.Empty;
    public string? PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientNote { get; set; } = string.Empty;
    public string? DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string DoctorPhone { get; set; } = string.Empty;
    public string DoctorEmail { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string SpecialtyName { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string AppointmentType { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public string Duration { get; set; } = string.Empty;
    public string ConsultationFee { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string CancelReason { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public AppointmentStatus Status { get; set; }
}

public class SelectOption
{
    public string Value { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class AppointmentCreateViewModel
{
    [Required]
    [Display(Name = "Tên bệnh nhân")]
    public string PatientName { get; set; } = string.Empty;

    [Display(Name = "Số điện thoại")]
    public string? PatientPhone { get; set; }

    [Required]
    [Display(Name = "Bác sĩ")]
    public string DoctorName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Chuyên khoa")]
    public string SpecialtyName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Ngày khám")]
    public DateOnly Date { get; set; }

    [Required]
    [Display(Name = "Giờ khám")]
    public TimeOnly Time { get; set; }

    [Display(Name = "Trạng thái")]
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    public IReadOnlyList<SelectOption> Doctors { get; set; } = Array.Empty<SelectOption>();
    public IReadOnlyList<SelectOption> Specialties { get; set; } = Array.Empty<SelectOption>();
}
