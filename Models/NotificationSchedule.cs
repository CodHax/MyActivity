using SQLite;

namespace MyActivity.Models;

/// <summary>
/// Bookkeeping for every scheduled local notification. The row Id IS the OS notification id,
/// and (UserId, Category, ReferenceKey) is unique, so duplicates cannot exist.
/// </summary>
[Table("NotificationSchedules")]
public class NotificationSchedule
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int UserId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ReferenceKey { get; set; } = string.Empty;

    /// <summary>UTC instant the notification fires.</summary>
    public DateTime FireAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
