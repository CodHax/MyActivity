using System.Globalization;
using SQLite;

namespace MyActivity.Models;

[Table("Meetings")]
public class Meeting
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>"yyyy-MM-dd".</summary>
    public string MeetingDate { get; set; } = string.Empty;

    /// <summary>"HH:mm".</summary>
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;

    public string? Location { get; set; }
    public string? Participants { get; set; }
    public string? Notes { get; set; }

    /// <summary>Minutes before start; -1 = no reminder.</summary>
    public int ReminderMinutes { get; set; } = -1;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [Ignore] public DateTime StartLocal => Combine(MeetingDate, StartTime);
    [Ignore] public DateTime EndLocal => Combine(MeetingDate, EndTime);

    private static DateTime Combine(string date, string time) =>
        DateTime.ParseExact($"{date} {time}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
