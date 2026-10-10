namespace MyActivity.Constants;

public static class AccountConstants
{
    public const string Credit = "Credit";
    public const string Debit = "Debit";

    public static readonly string[] Categories =
    {
        "Salary", "Bonus", "Reimbursement", "Food", "Travel", "Rent", "Bills",
        "Shopping", "Health", "Education", "Savings", "Other"
    };

    public const decimal MaxAmount = 99_99_99_999.99m;   // 99,99,99,999.99
}

public static class MeetingConstants
{
    public const string CategoryReminder = "Meeting.Reminder";
    public const string CategoryPrefix = "Meeting.";
    public const string PayloadPrefix = "mtg";

    /// <summary>(minutes before start, label). -1 = no reminder.</summary>
    public static readonly (int Minutes, string Text)[] ReminderOptions =
    {
        (-1, "No reminder"), (0, "At start time"), (5, "5 minutes before"), (10, "10 minutes before"),
        (15, "15 minutes before"), (30, "30 minutes before"), (60, "1 hour before"), (1440, "1 day before")
    };

    public static string DescribeLead(int minutes) => minutes switch
    {
        0 => "now",
        60 => "1 hour",
        1440 => "1 day",
        1 => "1 minute",
        _ => $"{minutes} minutes"
    };
}

public static class TaskConstants
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Pending = "Pending";
    public const string Completed = "Completed";

    public const string CategoryReminder = "Task.Reminder";
    public const string CategorySnooze = "Task.Snooze";
    public const string CategoryPrefix = "Task.";
    public const string PayloadPrefix = "task";

    public const int ActionComplete = 201;
    public const int ActionSnooze = 202;
    public const int SnoozeMinutes = 10;

    public static bool IsValidPriority(string? p) => p is Low or Medium or High;
    public static bool IsValidStatus(string? s) => s is Pending or Completed;
}
