using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Models;

namespace MyActivity.ViewModels;

public class TransactionItem
{
    public TransactionItem(AccountTransaction t, Action<TransactionItem> edit, Action<TransactionItem> delete)
    {
        Id = t.Id;
        Description = t.Description;
        CategoryText = t.Category;
        var date = DateTime.ParseExact(t.TransactionDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var time = DateTime.ParseExact(t.TransactionTime, "HH:mm", CultureInfo.InvariantCulture);
        DateText = $"{date.ToString("d MMM yyyy", CultureInfo.CurrentCulture)}, {time.ToString("hh:mm tt", CultureInfo.CurrentCulture)}";
        IsCredit = t.IsCredit;
        AmountText = (IsCredit ? "+ " : "- ") + MoneyFormat.Format(t.Amount);
        AmountColor = IsCredit ? Palette.Success : Palette.Danger;
        EditCommand = new RelayCommand(() => edit(this));
        DeleteCommand = new RelayCommand(() => delete(this));
    }

    public int Id { get; }
    public string Description { get; }
    public string CategoryText { get; }
    public string DateText { get; }
    public bool IsCredit { get; }
    public string AmountText { get; }
    public Color AmountColor { get; }
    public IRelayCommand EditCommand { get; }
    public IRelayCommand DeleteCommand { get; }
}

public class MeetingItem
{
    public MeetingItem(Meeting m, Action<MeetingItem> open)
    {
        Id = m.Id;
        Title = m.Title;
        var start = m.StartLocal;
        DateText = start.ToString("ddd, d MMM yyyy", CultureInfo.CurrentCulture);
        TimeText = $"{start.ToString("hh:mm tt", CultureInfo.CurrentCulture)} - {m.EndLocal.ToString("hh:mm tt", CultureInfo.CurrentCulture)}";
        LocationText = m.Location ?? string.Empty;
        HasLocation = !string.IsNullOrWhiteSpace(m.Location);
        ReminderText = m.ReminderMinutes < 0
            ? "No reminder"
            : MeetingConstants.ReminderOptions.First(o => o.Minutes == m.ReminderMinutes).Text;
        OpenCommand = new RelayCommand(() => open(this));
    }

    public int Id { get; }
    public string Title { get; }
    public string DateText { get; }
    public string TimeText { get; }
    public string LocationText { get; }
    public bool HasLocation { get; }
    public string ReminderText { get; }
    public IRelayCommand OpenCommand { get; }
}

public class TaskRowItem
{
    public TaskRowItem(TaskItem t, Action<TaskRowItem> toggle, Action<TaskRowItem> edit, Action<TaskRowItem> delete)
    {
        Id = t.Id;
        Title = t.Title;
        IsCompleted = t.IsCompleted;
        TitleDecorations = t.IsCompleted ? TextDecorations.Strikethrough : TextDecorations.None;
        PriorityText = t.Priority;
        PriorityColor = t.Priority switch
        {
            TaskConstants.High => Palette.Danger,
            TaskConstants.Medium => Palette.Warning,
            _ => Palette.Neutral
        };

        var parts = new List<string>();
        if (t.TaskDate is not null)
            parts.Add("Due " + DateTime.ParseExact(t.TaskDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                .ToString("d MMM", CultureInfo.CurrentCulture));
        if (t.ReminderLocal is { } r) parts.Add("Reminder " + AttendanceFormat.FormatWhen(r));
        DetailText = string.Join("  |  ", parts);
        HasDetail = parts.Count > 0;
        DescriptionText = t.Description ?? string.Empty;
        HasDescription = !string.IsNullOrWhiteSpace(t.Description);
        ToggleText = t.IsCompleted ? "Undo" : "Done";

        ToggleCommand = new RelayCommand(() => toggle(this));
        EditCommand = new RelayCommand(() => edit(this));
        DeleteCommand = new RelayCommand(() => delete(this));
    }

    public int Id { get; }
    public string Title { get; }
    public bool IsCompleted { get; }
    public TextDecorations TitleDecorations { get; }
    public string PriorityText { get; }
    public Color PriorityColor { get; }
    public string DetailText { get; }
    public bool HasDetail { get; }
    public string DescriptionText { get; }
    public bool HasDescription { get; }
    public string ToggleText { get; }
    public IRelayCommand ToggleCommand { get; }
    public IRelayCommand EditCommand { get; }
    public IRelayCommand DeleteCommand { get; }
}
