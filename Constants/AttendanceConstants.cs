namespace MyActivity.Constants;

public static class AttendanceConstants
{
    // Event types
    public const string CheckIn = "CheckIn";
    public const string CheckOut = "CheckOut";

    // Status
    public const string Present = "Present";
    public const string Absent = "Absent";

    // Source of the response
    public const string SourceNotification = "Notification";
    public const string SourceManual = "Manual";

    // Notification payload: "att|{userId}|{eventType}|{yyyy-MM-dd}"
    public const string PayloadPrefix = "att";

    // Notification action ids
    public const int ActionPresent = 101;
    public const int ActionAbsent = 102;
    public const int ActionSnooze = 103;

    public const int SnoozeMinutes = 5;
    public const int ScheduleWindowDays = 14;

    // NotificationSchedules.Category values
    public const string CategoryCheckIn = "Attendance.CheckIn";
    public const string CategoryCheckOut = "Attendance.CheckOut";
    public const string CategorySnooze = "Attendance.Snooze";
    public const string CategoryPrefix = "Attendance.";

    public static bool IsValidType(string? t) => t is CheckIn or CheckOut;
    public static bool IsValidStatus(string? s) => s is Present or Absent;
    public static string DateKey(DateTime d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
