namespace Web_Admin_Booking_App.Services;

public sealed class FirebaseSettings
{
    public const string SectionName = "Firebase";

    public string ProjectId { get; set; } = string.Empty;
    public string WebApiKey { get; set; } = string.Empty;

    public string ServiceAccountPath { get; set; } = string.Empty;
    public string ServiceAccountJson { get; set; } = string.Empty;

    public string[] UserCollections { get; set; } = ["users"];
    public string[] DoctorCollections { get; set; } = ["Doctors"];
    public string[] DepartmentCollections { get; set; } = ["Departments"];
    public string[] AppointmentCollections { get; set; } = ["Appointments"];
    public string[] InvoiceCollections { get; set; } = ["Invoices"];
    public string[] PaymentCollections { get; set; } = ["Payments"];
    public string[] DoctorScheduleCollections { get; set; } = ["DoctorSchedules"];
    public string[] ShiftCollections { get; set; } = ["Shifts"];
    public string[] NotificationCollections { get; set; } = ["Notifications"];
    public string[] NotificationTemplateCollections { get; set; } = ["notification_templates"];
    public string[] HealthInsuranceCollections { get; set; } = ["health_insurances"];
    public string[] MedicineCollections { get; set; } = ["Medicines", "medicines", "Drugs", "drugs", "Medications", "medications"];
}
