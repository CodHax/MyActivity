using SQLite;

namespace MyActivity.Models;

/// <summary>One row per employee / date / event (CheckIn or CheckOut).</summary>
[Table("Attendance")]
public class Attendance
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Local calendar date as "yyyy-MM-dd" (sortable, timezone-proof).</summary>
    public string AttendanceDate { get; set; } = string.Empty;

    /// <summary>CheckIn or CheckOut.</summary>
    public string AttendanceType { get; set; } = string.Empty;

    /// <summary>UTC instant; set when a CheckIn event was marked Present.</summary>
    public DateTime? CheckInTime { get; set; }

    /// <summary>UTC instant; set when a CheckOut event was marked Present.</summary>
    public DateTime? CheckOutTime { get; set; }

    /// <summary>UTC instant the employee responded.</summary>
    public DateTime ResponseTime { get; set; }

    /// <summary>Present or Absent.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Notification or Manual.</summary>
    public string Source { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Both events of one day (not a table).</summary>
public class AttendanceDay
{
    public DateTime Date { get; init; }
    public Attendance? CheckIn { get; init; }
    public Attendance? CheckOut { get; init; }
}
