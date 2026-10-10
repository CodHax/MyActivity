using Microsoft.Extensions.Logging;
using MyActivity.Models;
using MyActivity.Repositories;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;

namespace MyActivity.Services;

public enum NotificationActionSet { None, Attendance, Task }

public record NotificationJob(
    int UserId,
    string Category,
    string ReferenceKey,
    DateTime FireAtLocal,
    string Title,
    string Message,
    string? Data,
    NotificationActionSet Actions = NotificationActionSet.None);

/// <summary>
/// Generic local-notification layer (reused by Meetings and Tasks in later steps).
/// Every notification is tracked in NotificationSchedules; the row Id is the OS notification id.
/// </summary>
public interface INotificationService
{
    Task<bool> AreNotificationsEnabledAsync();
    Task<bool> RequestPermissionAsync();
    /// <summary>Schedules (or replaces) a notification. Returns its id, or null if it could not be scheduled.</summary>
    Task<int?> ScheduleAsync(NotificationJob job);
    Task CancelAsync(int userId, string category, string referenceKey);
    Task CancelCategoryAsync(int userId, string categoryPrefix);
    Task CancelAllForUserAsync(int userId);
    Task<DateTime?> GetNextFireLocalAsync(int userId, string categoryPrefix);
}

public class NotificationService : INotificationService
{
    private readonly INotificationScheduleRepository _schedules;
    private readonly ILogger<NotificationService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public NotificationService(INotificationScheduleRepository schedules, ILogger<NotificationService> logger)
    {
        _schedules = schedules;
        _logger = logger;
    }

    public async Task<bool> AreNotificationsEnabledAsync()
    {
        try { return await LocalNotificationCenter.Current.AreNotificationsEnabled(); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read notification permission");
            return false;
        }
    }

    public async Task<bool> RequestPermissionAsync()
    {
        try
        {
            var request = new NotificationPermission
            {
                Android = { RequestPermissionToScheduleExactAlarm = true }
            };
            var granted = await LocalNotificationCenter.Current.RequestNotificationPermission(request);

            // Android 12+: exact alarms are needed so reminders fire on the minute.
            var status = await LocalNotificationCenter.Current.GetNotificationPermissionStatus();
            if (status.IsEnabled && !status.CanScheduleExactAlarms &&
                LocalNotificationCenter.AndroidService is { } android)
            {
                await android.RequestExactAlarmsPermission();
            }
            return granted;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification permission request failed");
            return false;
        }
    }

    public async Task<int?> ScheduleAsync(NotificationJob job)
    {
        if (job.FireAtLocal <= DateTime.Now) return null;

        await _gate.WaitAsync();
        try
        {
            await CancelCoreAsync(await FindAsList(job.UserId, job.Category, job.ReferenceKey));

            var row = new NotificationSchedule
            {
                UserId = job.UserId,
                Category = job.Category,
                ReferenceKey = job.ReferenceKey,
                FireAt = job.FireAtLocal.ToUniversalTime(),
                CreatedAt = DateTime.UtcNow
            };
            await _schedules.InsertAsync(row);

            var request = new NotificationRequest
            {
                NotificationId = row.Id,
                Title = job.Title,
                Description = job.Message,
                ReturningData = job.Data ?? string.Empty,
                CategoryType = job.Actions switch
                {
                    NotificationActionSet.Attendance => NotificationCategoryType.Status,
                    NotificationActionSet.Task => NotificationCategoryType.Reminder,
                    _ => NotificationCategoryType.None
                },
                Schedule = { NotifyTime = new DateTimeOffset(job.FireAtLocal) }
            };

            var shown = await LocalNotificationCenter.Current.Show(request);
            if (!shown)
            {
                await _schedules.DeleteAsync(new[] { row.Id });
                return null;
            }
            return row.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduling notification failed ({Category})", job.Category);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CancelAsync(int userId, string category, string referenceKey)
    {
        await _gate.WaitAsync();
        try { await CancelCoreAsync(await FindAsList(userId, category, referenceKey)); }
        catch (Exception ex) { _logger.LogError(ex, "Cancel notification failed ({Category})", category); }
        finally { _gate.Release(); }
    }

    public async Task CancelCategoryAsync(int userId, string categoryPrefix)
    {
        await _gate.WaitAsync();
        try { await CancelCoreAsync(await _schedules.GetByCategoryPrefixAsync(userId, categoryPrefix)); }
        catch (Exception ex) { _logger.LogError(ex, "Cancel category failed ({Prefix})", categoryPrefix); }
        finally { _gate.Release(); }
    }

    public async Task CancelAllForUserAsync(int userId)
    {
        await _gate.WaitAsync();
        try { await CancelCoreAsync(await _schedules.GetForUserAsync(userId)); }
        catch (Exception ex) { _logger.LogError(ex, "Cancel all notifications failed"); }
        finally { _gate.Release(); }
    }

    public async Task<DateTime?> GetNextFireLocalAsync(int userId, string categoryPrefix)
    {
        var rows = await _schedules.GetByCategoryPrefixAsync(userId, categoryPrefix);
        var now = DateTime.UtcNow;
        var next = rows.Where(r => r.FireAt > now).OrderBy(r => r.FireAt).FirstOrDefault();
        return next is null ? null : DateTime.SpecifyKind(next.FireAt, DateTimeKind.Utc).ToLocalTime();
    }

    private async Task<List<NotificationSchedule>> FindAsList(int userId, string category, string key)
    {
        var row = await _schedules.FindAsync(userId, category, key);
        return row is null ? new() : new() { row };
    }

    private async Task CancelCoreAsync(IReadOnlyCollection<NotificationSchedule> rows)
    {
        if (rows.Count == 0) return;
        var ids = rows.Select(r => r.Id).ToArray();
        try { await LocalNotificationCenter.Current.Cancel(ids); }
        catch (Exception ex) { _logger.LogWarning(ex, "OS cancel failed"); }
        await _schedules.DeleteAsync(ids);
    }
}
