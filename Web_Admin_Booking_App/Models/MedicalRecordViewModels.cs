namespace Web_Admin_Booking_App.Models;

public enum MedicalRecordStatus
{
    Pending = 1,
    Approved = 2,
    Cancelled = 3,
}

public class MedicalRecordsIndexViewModel
{
    public string? Search { get; set; }
    public string? StatusFilter { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? FilterError { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize)));
    public int StartItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int EndItem => Math.Min(TotalCount, Page * PageSize);
    public IReadOnlyList<MedicalRecordListItemViewModel> Items { get; set; } = Array.Empty<MedicalRecordListItemViewModel>();
}

public class MedicalRecordListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string RecordCode { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public string SpecialtyName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public MedicalRecordStatus Status { get; set; }
}

public class MedicalRecordDetailsViewModel
{
    public string Id { get; set; } = string.Empty;
    public string RecordCode { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string SpecialtyName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public MedicalRecordStatus Status { get; set; }

    public string? Diagnosis { get; set; }
    public string? ClinicalNotes { get; set; }
    public string? Prescription { get; set; }
}
