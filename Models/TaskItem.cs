using SQLite;

namespace MyActivity.Models;

/// <summary>Named TaskItem (not Task) to avoid clashing with System.Threading.Tasks.Task.</summary>
[Table("TaskItems")]
public class TaskItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Optional due date, "yyyy-MM-dd".</summary>
    public string? TaskDate { get; set; }

    /// <summary>Optional reminder instant (UTC).</summary>
    public DateTime? ReminderAt { get; set; }

    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = "Pending";
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [Ignore] public bool IsCompleted => Status == "Completed";
    [Ignore] public DateTime? ReminderLocal =>
        ReminderAt is null ? null : DateTime.SpecifyKind(ReminderAt.Value, DateTimeKind.Utc).ToLocalTime();
}
