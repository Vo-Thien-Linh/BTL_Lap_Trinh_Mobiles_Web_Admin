namespace Web_Admin_Booking_App.Models;

public enum MedicalRecordStatus
{
    Pending = 1,
    Approved = 2,
    Cancelled = 3,
}

public class MedicalRecordListItemViewModel
{
    public int Id { get; set; }
    public string RecordCode { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public string SpecialtyName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public MedicalRecordStatus Status { get; set; }
}

public class MedicalRecordDetailsViewModel
{
    public int Id { get; set; }
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
