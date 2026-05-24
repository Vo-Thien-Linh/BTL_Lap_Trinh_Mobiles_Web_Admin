namespace Web_Admin_Booking_App.Services;

public sealed class FirebaseSettings
{
    public const string SectionName = "Firebase";

    public string ProjectId { get; set; } = string.Empty;
    public string WebApiKey { get; set; } = string.Empty;

    // Khuyến nghị: đặt file service account ngoài Git hoặc dùng User Secrets.
    public string ServiceAccountPath { get; set; } = string.Empty;
    public string ServiceAccountJson { get; set; } = string.Empty;

    // Project Flutter trước đó có lúc dùng chữ hoa/chữ thường, nên web admin hỗ trợ cả hai.
    public string[] UserCollections { get; set; } = ["users", "Users"];
    public string[] DoctorCollections { get; set; } = ["Doctors", "doctors"];
    public string[] DepartmentCollections { get; set; } = ["Departments", "departments"];
    public string[] AppointmentCollections { get; set; } = ["Appointments", "appointments"];
    public string[] InvoiceCollections { get; set; } = ["Invoices", "invoices"];
    public string[] PaymentCollections { get; set; } = ["Payments", "payments"];
    public string[] DoctorScheduleCollections { get; set; } = ["DoctorSchedules", "doctorSchedules", "doctor_schedules"];
    public string[] ShiftCollections { get; set; } = ["Shifts", "shifts"];
    public string[] NotificationCollections { get; set; } = ["Notifications", "notifications"];
    public string[] NotificationTemplateCollections { get; set; } = ["notification_templates", "NotificationTemplates"];
    public string[] PatientCollections { get; set; } = ["patients", "Patients"];
}
