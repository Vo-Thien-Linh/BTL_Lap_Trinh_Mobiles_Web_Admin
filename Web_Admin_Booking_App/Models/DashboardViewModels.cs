namespace Web_Admin_Booking_App.Models;

public sealed class DashboardViewModel
{
    public int PatientCount { get; set; }
    public decimal PatientGrowthPercent { get; set; }
    public int TodayAppointmentCount { get; set; }
    public int WaitingAppointmentCount { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public int OnDutyDoctorCount { get; set; }
    public int TotalDoctorCount { get; set; }
    public IReadOnlyList<DashboardRevenuePointViewModel> ThisWeekRevenue { get; set; } = Array.Empty<DashboardRevenuePointViewModel>();
    public IReadOnlyList<DashboardRevenuePointViewModel> LastWeekRevenue { get; set; } = Array.Empty<DashboardRevenuePointViewModel>();
    public IReadOnlyList<DashboardAppointmentViewModel> RecentAppointments { get; set; } = Array.Empty<DashboardAppointmentViewModel>();
    public IReadOnlyList<DashboardReminderViewModel> Reminders { get; set; } = Array.Empty<DashboardReminderViewModel>();
}

public sealed class DashboardRevenuePointViewModel
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class DashboardAppointmentViewModel
{
    public string Id { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public AppointmentStatus Status { get; set; }
}

public sealed class DashboardReminderViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tone { get; set; } = "secondary";
}
