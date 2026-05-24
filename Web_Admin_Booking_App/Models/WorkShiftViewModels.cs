namespace Web_Admin_Booking_App.Models;

public enum ShiftType
{
    Morning = 1,
    Afternoon = 2,
    Night = 3,
}

public enum AssignmentStatus
{
    Assigned = 1,
    Pending = 2,
}

public class WorkShiftsIndexViewModel
{
    public string Mode { get; set; } = "calendar"; // calendar | list
    public string RangeLabel { get; set; } = string.Empty;
    public WorkScheduleGenerateViewModel GenerateForm { get; set; } = new();
    public IReadOnlyList<SelectOption> Doctors { get; set; } = Array.Empty<SelectOption>();
    public IReadOnlyList<SelectOption> Departments { get; set; } = Array.Empty<SelectOption>();
    public IReadOnlyDictionary<string, IReadOnlyList<string>> DepartmentRooms { get; set; } = new Dictionary<string, IReadOnlyList<string>>();
    public IReadOnlyList<SelectOption> Shifts { get; set; } = Array.Empty<SelectOption>();

    public string TotalDoctorsLabel { get; set; } = string.Empty;
    public int UnassignedCount { get; set; }
    public decimal FillRatePercent { get; set; }
    public int ConflictCount { get; set; }

    public string MonthLabel { get; set; } = string.Empty;
    public CalendarMonthViewModel Calendar { get; set; } = new();

    public string TodayLabel { get; set; } = string.Empty;
    public IReadOnlyList<TodayAssignmentViewModel> TodayAssignments { get; set; } = Array.Empty<TodayAssignmentViewModel>();
    public IReadOnlyList<DoctorScheduleRowViewModel> Schedules { get; set; } = Array.Empty<DoctorScheduleRowViewModel>();
}

public class WorkScheduleGenerateViewModel
{
    public string DoctorId { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public List<string> ShiftIds { get; set; } = new();
    public List<DayOfWeek> DaysOfWeek { get; set; } = new();
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int WeeksAhead { get; set; } = 4;
    public int AvailableSlots { get; set; } = 10;
}

public class WorkScheduleBackfillRoomViewModel
{
    public string DoctorId { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
}

public class DoctorScheduleRowViewModel
{
    public string DocumentId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public string ShiftTime { get; set; } = string.Empty;
    public int AvailableSlots { get; set; }
    public bool IsActive { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CalendarMonthViewModel
{
    public DateOnly MonthStart { get; set; }
    public IReadOnlyList<CalendarWeekViewModel> Weeks { get; set; } = Array.Empty<CalendarWeekViewModel>();
}

public class CalendarWeekViewModel
{
    public IReadOnlyList<CalendarDayViewModel> Days { get; set; } = Array.Empty<CalendarDayViewModel>();
}

public class CalendarDayViewModel
{
    public DateOnly Date { get; set; }
    public bool IsInCurrentMonth { get; set; }
    public bool IsToday { get; set; }
    public IReadOnlyList<ShiftEventViewModel> Events { get; set; } = Array.Empty<ShiftEventViewModel>();
}

public class ShiftEventViewModel
{
    public ShiftType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
}

public class TodayAssignmentViewModel
{
    public string Initial { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public string? DoctorTitle { get; set; }
    public string Specialty { get; set; } = string.Empty;
    public string ShiftLabel { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public AssignmentStatus Status { get; set; }
}

public static class CalendarBuilder
{
    public static CalendarMonthViewModel BuildMonth(DateOnly monthStart)
    {
        // Calendar starts on Monday
        var first = monthStart;
        var firstDow = DayOfWeekToMondayBased(first.DayOfWeek);
        var gridStart = first.AddDays(-firstDow);

        var weeks = new List<CalendarWeekViewModel>();
        var cursor = gridStart;

        for (var w = 0; w < 6; w++)
        {
            var days = new List<CalendarDayViewModel>();
            for (var d = 0; d < 7; d++)
            {
                var date = cursor;
                days.Add(new CalendarDayViewModel
                {
                    Date = date,
                    IsInCurrentMonth = date.Month == monthStart.Month,
                    IsToday = date == DateOnly.FromDateTime(DateTime.Today),
                    Events = new List<ShiftEventViewModel>(),
                });
                cursor = cursor.AddDays(1);
            }

            weeks.Add(new CalendarWeekViewModel { Days = days });
        }

        return new CalendarMonthViewModel
        {
            MonthStart = monthStart,
            Weeks = weeks,
        };
    }

    public static void AddShift(CalendarMonthViewModel month, DateOnly date, ShiftType type, string title, string? subtitle = null)
    {
        foreach (var week in month.Weeks)
        {
            foreach (var day in week.Days)
            {
                if (day.Date != date) continue;

                var list = day.Events.ToList();
                list.Add(new ShiftEventViewModel { Type = type, Title = title, Subtitle = subtitle });
                day.Events = list;
                return;
            }
        }
    }

    private static int DayOfWeekToMondayBased(DayOfWeek dayOfWeek)
    {
        // Monday=0..Sunday=6
        return dayOfWeek switch
        {
            DayOfWeek.Monday => 0,
            DayOfWeek.Tuesday => 1,
            DayOfWeek.Wednesday => 2,
            DayOfWeek.Thursday => 3,
            DayOfWeek.Friday => 4,
            DayOfWeek.Saturday => 5,
            DayOfWeek.Sunday => 6,
            _ => 0,
        };
    }
}
