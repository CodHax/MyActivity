using Microsoft.Extensions.Logging;

namespace MyActivity.Services;

/// <summary>Single place that (re)builds or removes every local reminder for a user.</summary>
public interface IReminderCoordinator
{
    Task RescheduleAllAsync(int userId);
    Task CancelAllAsync(int userId);
}

public class ReminderCoordinator : IReminderCoordinator
{
    private readonly IAttendanceService _attendance;
    private readonly IMeetingService _meetings;
    private readonly ITaskService _tasks;
    private readonly INotificationService _notifications;
    private readonly ILogger<ReminderCoordinator> _logger;

    public ReminderCoordinator(IAttendanceService attendance, IMeetingService meetings, ITaskService tasks,
        INotificationService notifications, ILogger<ReminderCoordinator> logger)
    {
        _attendance = attendance;
        _meetings = meetings;
        _tasks = tasks;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task RescheduleAllAsync(int userId)
    {
        // One failing module must not stop the others.
        await Safe(() => _attendance.RescheduleAsync(userId), "attendance");
        await Safe(() => _meetings.RescheduleAllAsync(userId), "meetings");
        await Safe(() => _tasks.RescheduleAllAsync(userId), "tasks");
    }

    public Task CancelAllAsync(int userId) => _notifications.CancelAllForUserAsync(userId);

    private async Task Safe(Func<Task> work, string name)
    {
        try { await work(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Rescheduling {Module} reminders failed", name); }
    }
}
