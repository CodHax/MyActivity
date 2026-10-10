using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Messages;
using MyActivity.Models;
using MyActivity.Repositories;

namespace MyActivity.Services;

public record TaskInput(string? Title, string? Description, DateTime? Date, DateTime? ReminderLocal,
    string Priority, string Status);

public interface ITaskService
{
    Task<ServiceResult<TaskItem>> SaveAsync(int userId, int? id, TaskInput input);
    Task<ServiceResult> SetCompletedAsync(int userId, int id, bool completed);
    Task<ServiceResult> DeleteAsync(int userId, int id);
    Task<TaskItem?> GetAsync(int userId, int id);
    Task<List<TaskItem>> ListAsync(int userId, string? status);
    Task<TaskItem?> GetNextAsync(int userId);
    Task RescheduleAllAsync(int userId);
    Task HandleNotificationActionAsync(int actionId, string? payload);
}

public class TaskService : ITaskService
{
    private readonly ITaskRepository _repo;
    private readonly ISettingsService _settings;
    private readonly INotificationService _notifications;
    private readonly ILogger<TaskService> _logger;

    public TaskService(ITaskRepository repo, ISettingsService settings,
        INotificationService notifications, ILogger<TaskService> logger)
    {
        _repo = repo;
        _settings = settings;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<ServiceResult<TaskItem>> SaveAsync(int userId, int? id, TaskInput i)
    {
        try
        {
            TaskItem? existing = null;
            if (id is not null)
            {
                existing = await _repo.GetAsync(userId, id.Value);
                if (existing is null) return ServiceResult<TaskItem>.Fail("This task no longer exists.");
            }

            var error = Validate(i, existing);
            if (error is not null) return ServiceResult<TaskItem>.Fail(error);

            var now = DateTime.UtcNow;
            var t = existing ?? new TaskItem { UserId = userId, CreatedAt = now };
            var wasCompleted = t.IsCompleted;

            t.Title = i.Title!.Trim();
            t.Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim();
            t.TaskDate = i.Date is null ? null : AttendanceConstants.DateKey(i.Date.Value);
            t.ReminderAt = i.ReminderLocal?.ToUniversalTime();
            t.Priority = i.Priority;
            t.Status = i.Status;
            t.CompletedAt = i.Status == TaskConstants.Completed ? (wasCompleted ? t.CompletedAt : now) : null;
            t.UpdatedAt = now;

            if (existing is null) await _repo.InsertAsync(t); else await _repo.UpdateAsync(t);

            var (status, fire) = await ScheduleAsync(userId, t);
            WeakReferenceMessenger.Default.Send(new TasksChangedMessage());

            var message = "Task saved successfully." + status switch
            {
                ReminderStatus.Scheduled => $" Task reminder scheduled for {AttendanceFormat.FormatWhen(fire!.Value)}.",
                ReminderStatus.Disabled => " Turn on notifications to receive task reminders.",
                _ => string.Empty
            };
            return ServiceResult<TaskItem>.Ok(t, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving task failed");
            return ServiceResult<TaskItem>.Fail(AppConstants.Messages.SaveFailed);
        }
    }

    public async Task<ServiceResult> SetCompletedAsync(int userId, int id, bool completed)
    {
        try
        {
            var t = await _repo.GetAsync(userId, id);
            if (t is null) return ServiceResult.Fail("This task no longer exists.");

            t.Status = completed ? TaskConstants.Completed : TaskConstants.Pending;
            t.CompletedAt = completed ? DateTime.UtcNow : null;
            t.UpdatedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(t);

            await ScheduleAsync(userId, t);   // cancels when completed; restores a still-future reminder when reopened
            WeakReferenceMessenger.Default.Send(new TasksChangedMessage());
            return ServiceResult.Ok(completed ? "Task marked as completed." : "Task moved back to pending.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Updating task status failed");
            return ServiceResult.Fail(AppConstants.Messages.SaveFailed);
        }
    }

    public async Task<ServiceResult> DeleteAsync(int userId, int id)
    {
        try
        {
            await CancelAllFor(userId, id);
            if (!await _repo.DeleteAsync(userId, id)) return ServiceResult.Fail("This task no longer exists.");
            WeakReferenceMessenger.Default.Send(new TasksChangedMessage());
            return ServiceResult.Ok("Task deleted.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deleting task failed");
            return ServiceResult.Fail(AppConstants.Messages.SaveFailed);
        }
    }

    public Task<TaskItem?> GetAsync(int userId, int id) => _repo.GetAsync(userId, id);
    public Task<List<TaskItem>> ListAsync(int userId, string? status) => _repo.QueryAsync(userId, status);

    public Task<TaskItem?> GetNextAsync(int userId) =>
        _repo.GetNextAsync(userId, DateTime.UtcNow, AttendanceConstants.DateKey(DateTime.Today));

    public async Task RescheduleAllAsync(int userId)
    {
        await _notifications.CancelCategoryAsync(userId, TaskConstants.CategoryReminder);
        foreach (var t in await _repo.GetPendingWithFutureReminderAsync(userId, DateTime.UtcNow))
            await ScheduleAsync(userId, t);
    }

    /// <summary>Notification buttons: "Mark complete" and "Snooze 10 min".</summary>
    public async Task HandleNotificationActionAsync(int actionId, string? payload)
    {
        if (!NotificationPayload.TryParse(payload, TaskConstants.PayloadPrefix, out var userId, out var id)) return;

        switch (actionId)
        {
            case TaskConstants.ActionComplete:
                await SetCompletedAsync(userId, id, true);
                break;

            case TaskConstants.ActionSnooze:
                var t = await _repo.GetAsync(userId, id);
                if (t is null || t.IsCompleted) return;
                await _notifications.ScheduleAsync(BuildJob(userId, t, TaskConstants.CategorySnooze,
                    DateTime.Now.AddMinutes(TaskConstants.SnoozeMinutes)));
                break;
        }
    }

    private async Task<(ReminderStatus Status, DateTime? Fire)> ScheduleAsync(int userId, TaskItem t)
    {
        await CancelAllFor(userId, t.Id);
        if (t.IsCompleted || t.ReminderLocal is not { } fire) return (ReminderStatus.None, null);
        if (fire <= DateTime.Now) return (ReminderStatus.TimePassed, null);

        var s = await _settings.GetAsync(userId);
        if (!s.NotificationsEnabled || !s.TaskRemindersEnabled || !await _notifications.AreNotificationsEnabledAsync())
            return (ReminderStatus.Disabled, null);

        var id = await _notifications.ScheduleAsync(BuildJob(userId, t, TaskConstants.CategoryReminder, fire));
        return id is null ? (ReminderStatus.Disabled, null) : (ReminderStatus.Scheduled, fire);
    }

    private async Task CancelAllFor(int userId, int taskId)
    {
        await _notifications.CancelAsync(userId, TaskConstants.CategoryReminder, taskId.ToString());
        await _notifications.CancelAsync(userId, TaskConstants.CategorySnooze, taskId.ToString());
    }

    private static NotificationJob BuildJob(int userId, TaskItem t, string category, DateTime fire) =>
        new(userId, category, t.Id.ToString(), fire, "Task Reminder", t.Title,
            NotificationPayload.Build(TaskConstants.PayloadPrefix, userId, t.Id), NotificationActionSet.Task);

    private static string? Validate(TaskInput i, TaskItem? existing)
    {
        if (string.IsNullOrWhiteSpace(i.Title)) return "Please enter a task title.";
        if (i.Title.Trim().Length > 120) return "Title must be 120 characters or fewer.";
        if (i.Description is { Length: > 2000 }) return "Description must be 2000 characters or fewer.";
        if (!TaskConstants.IsValidPriority(i.Priority)) return "Please choose a priority.";
        if (!TaskConstants.IsValidStatus(i.Status)) return "Please choose a status.";

        if (i.ReminderLocal is { } r && i.Status == TaskConstants.Pending && r <= DateTime.Now)
        {
            // Editing a task whose (already fired) reminder is untouched must still be saveable.
            var unchanged = existing?.ReminderLocal is { } old && Math.Abs((old - r).TotalMinutes) < 1;
            if (!unchanged) return "Reminder time must be in the future.";
        }
        return null;
    }
}
