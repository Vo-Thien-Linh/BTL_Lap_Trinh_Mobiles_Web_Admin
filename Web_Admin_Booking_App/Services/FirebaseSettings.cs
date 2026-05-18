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
    public string[] UserCollections { get; set; } = ["Users", "users"];
    public string[] DoctorCollections { get; set; } = ["Doctors", "doctors"];
    public string[] DepartmentCollections { get; set; } = ["Departments", "departments"];
    public string[] AppointmentCollections { get; set; } = ["Appointments", "appointments"];
    public string[] InvoiceCollections { get; set; } = ["Invoices", "invoices"];
}
