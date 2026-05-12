namespace Web_Admin_Booking_App.Models;

public enum Gender
{
    Unknown = 0,
    Male = 1,
    Female = 2,
}

public enum PatientStatus
{
    Active = 1,
    Inactive = 2,
}

public class PatientListItemViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateOnly Dob { get; set; }
    public PatientStatus Status { get; set; }
}

public class PatientDetailsViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public Gender Gender { get; set; }
    public DateOnly Dob { get; set; }
    public string? Address { get; set; }
    public PatientStatus Status { get; set; }
    public DateTime? LastVisitAt { get; set; }
    public string? Notes { get; set; }
}
