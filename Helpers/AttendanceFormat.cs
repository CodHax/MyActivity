using System.Globalization;
using MyActivity.Constants;
using MyActivity.Models;

namespace MyActivity.Helpers;

public static class AttendanceFormat
{
    public static string Time(DateTime? utc) =>
        utc is null ? "--:--"
            : DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToLocalTime()
                .ToString("hh:mm tt", CultureInfo.CurrentCulture);

    public static string TimeOfDay(int minutesAfterMidnight) =>
        DateTime.Today.AddMinutes(minutesAfterMidnight).ToString("hh:mm tt", CultureInfo.CurrentCulture);

    public static string Time(TimeSpan t) =>
        DateTime.Today.Add(t).ToString("hh:mm tt", CultureInfo.CurrentCulture);

    public static string StatusText(Attendance? a, bool isToday) =>
        a?.Status ?? (isToday ? "Pending" : "Not marked");

    public static Color StatusColor(Attendance? a) => a?.Status switch
    {
        AttendanceConstants.Present => Color.FromArgb("#2F9E44"),
        AttendanceConstants.Absent => Color.FromArgb("#D9363E"),
        _ => Color.FromArgb("#8A94A8")
    };

    public static string FormatWhen(DateTime local)
    {
        var day = local.Date == DateTime.Today ? "Today"
                : local.Date == DateTime.Today.AddDays(1) ? "Tomorrow"
                : local.ToString("ddd, d MMM", CultureInfo.CurrentCulture);
        return $"{day}, {local.ToString("hh:mm tt", CultureInfo.CurrentCulture)}";
    }
}
