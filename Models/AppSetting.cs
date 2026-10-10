using MyActivity.Constants;
using SQLite;

namespace MyActivity.Models;

[Table("AppSettings")]
public class AppSetting
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>FK -> Users.Id (one settings row per user).</summary>
    public int UserId { get; set; }

    public string Theme { get; set; } = AppConstants.Themes.System;
    public bool NotificationsEnabled { get; set; } = true;
    public bool AttendanceRemindersEnabled { get; set; } = true;
    public bool MeetingRemindersEnabled { get; set; } = true;
    public bool TaskRemindersEnabled { get; set; } = true;
    public int SnoozeMinutes { get; set; } = 5;

    /// <summary>Scheduled check-in, minutes after midnight (510 = 08:30).</summary>
    public int CheckInMinutes { get; set; } = 510;

    /// <summary>Scheduled check-out, minutes after midnight (1050 = 17:30).</summary>
    public int CheckOutMinutes { get; set; } = 1050;

    /// <summary>Bit per weekday, bit = (int)DayOfWeek (Sun=0). Default Mon-Fri = 62.</summary>
    public int WorkingDaysMask { get; set; } = 62;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static AppSetting CreateDefault(int userId) => new() { UserId = userId };
}
