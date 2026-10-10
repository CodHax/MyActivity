using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Messages;
using MyActivity.Models;
using MyActivity.Repositories;

namespace MyActivity.Services;

public interface IAttendanceService
{
    Task<AppSetting> GetSettingsAsync(int userId);
    Task<ServiceResult> SaveScheduleAsync(int userId, TimeSpan checkIn, TimeSpan checkOut, int workingDaysMask, bool remindersEnabled);

    Task<ServiceResult> MarkAsync(int userId, string eventType, string status, DateTime? date = null,
        bool overwrite = true, string source = AttendanceConstants.SourceManual);

    Task<AttendanceDay> GetDayAsync(int userId, DateTime date);
    Task<IReadOnlyList<AttendanceDay>> GetDaysAsync(int userId, DateTime from, DateTime to);

    /// <summary>Rebuilds upcoming check-in/out reminders. Returns false if reminders are off or not permitted.</summary>
    Task<bool> RescheduleAsync(int userId);
    Task CancelAllAsync(int userId);
    Task<DateTime?> GetNextReminderAsync(int userId);

    /// <summary>Entry point for notification buttons (Present / Absent / Next 5 min).</summary>
    Task HandleNotificationActionAsync(int actionId, string? payload);
}

public class AttendanceService : IAttendanceService
{
    private readonly IAttendanceRepository _attendance;
    private readonly ISettingsRepository _settings;
    private readonly IUserRepository _users;
    private readonly INotificationService _notifications;
    private readonly ILogger<AttendanceService> _logger;
    private readonly SemaphoreSlim _scheduleGate = new(1, 1);

    public AttendanceService(IAttendanceRepository attendance, ISettingsRepository settings, IUserRepository users,
        INotificationService notifications, ILogger<AttendanceService> logger)
    {
        _attendance = attendance;
        _settings = settings;
        _users = users;
        _notifications = notifications;
        _logger = logger;
    }

    // ---------- Settings ----------
    public async Task<AppSetting> GetSettingsAsync(int userId)
    {
        var s = await _settings.GetByUserIdAsync(userId);
        if (s is not null) return s;
        s = AppSetting.CreateDefault(userId);
        await _settings.InsertAsync(s);
        return s;
    }

    public async Task<ServiceResult> SaveScheduleAsync(int userId, TimeSpan checkIn, TimeSpan checkOut,
        int workingDaysMask, bool remindersEnabled)
    {
        if (checkOut <= checkIn) return ServiceResult.Fail("Check-out time must be later than check-in time.");
        if ((workingDaysMask & 0b1111111) == 0) return ServiceResult.Fail("Select at least one working day.");

        try
        {
            var s = await GetSettingsAsync(userId);
            s.CheckInMinutes = (int)checkIn.TotalMinutes;
            s.CheckOutMinutes = (int)checkOut.TotalMinutes;
            s.WorkingDaysMask = workingDaysMask & 0b1111111;
            s.AttendanceRemindersEnabled = remindersEnabled;
            await _settings.UpdateAsync(s);

            var scheduled = await RescheduleAsync(userId);
            return ServiceResult.Ok(remindersEnabled && !scheduled
                ? "Settings saved. Turn on notifications in your phone settings to receive reminders."
                : "Attendance settings saved.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving attendance settings failed");
            return ServiceResult.Fail(AppConstants.Messages.SaveFailed);
        }
    }

    // ---------- Marking ----------
    public async Task<ServiceResult> MarkAsync(int userId, string eventType, string status, DateTime? date = null,
        bool overwrite = true, string source = AttendanceConstants.SourceManual)
    {
        if (!AttendanceConstants.IsValidType(eventType) || !AttendanceConstants.IsValidStatus(status))
            return ServiceResult.Fail(AppConstants.Messages.SaveFailed);

        try
        {
            var dateKey = AttendanceConstants.DateKey(date ?? DateTime.Today);
            var now = DateTime.UtcNow;
            var present = status == AttendanceConstants.Present;

            var record = new Attendance
            {
                UserId = userId,
                AttendanceDate = dateKey,
                AttendanceType = eventType,
                CheckInTime = present && eventType == AttendanceConstants.CheckIn ? now : null,
                CheckOutTime = present && eventType == AttendanceConstants.CheckOut ? now : null,
                ResponseTime = now,
                Status = status,
                Source = source,
                CreatedAt = now,
                UpdatedAt = now
            };

            var written = await _attendance.UpsertAsync(record, overwrite);

            // A response (even a rejected duplicate) means the event is settled: drop its pending reminders.
            await _notifications.CancelAsync(userId, AttendanceConstants.CategorySnooze, $"{dateKey}|{eventType}");
            var category = eventType == AttendanceConstants.CheckIn
                ? AttendanceConstants.CategoryCheckIn : AttendanceConstants.CategoryCheckOut;
            await _notifications.CancelAsync(userId, category, dateKey);

            if (!written) return ServiceResult.Fail("Attendance is already marked for this event.");

            WeakReferenceMessenger.Default.Send(new AttendanceChangedMessage());
            var label = eventType == AttendanceConstants.CheckIn ? "Check-in" : "Check-out";
            return ServiceResult.Ok($"{label} marked as {status}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Marking attendance failed");
            return ServiceResult.Fail(AppConstants.Messages.SaveFailed);
        }
    }

    // ---------- Queries ----------
    public async Task<AttendanceDay> GetDayAsync(int userId, DateTime date)
    {
        var key = AttendanceConstants.DateKey(date);
        return new AttendanceDay
        {
            Date = date.Date,
            CheckIn = await _attendance.GetAsync(userId, key, AttendanceConstants.CheckIn),
            CheckOut = await _attendance.GetAsync(userId, key, AttendanceConstants.CheckOut)
        };
    }

    public async Task<IReadOnlyList<AttendanceDay>> GetDaysAsync(int userId, DateTime from, DateTime to)
    {
        var rows = await _attendance.GetRangeAsync(userId,
            AttendanceConstants.DateKey(from), AttendanceConstants.DateKey(to));
        var byDate = rows.GroupBy(r => r.AttendanceDate).ToDictionary(g => g.Key, g => g.ToList());
        var mask = (await GetSettingsAsync(userId)).WorkingDaysMask;

        var today = DateTime.Today;
        var end = to.Date > today ? today : to.Date;
        var list = new List<AttendanceDay>();

        for (var d = end; d >= from.Date; d = d.AddDays(-1))
        {
            byDate.TryGetValue(AttendanceConstants.DateKey(d), out var recs);
            if (recs is null && d != today && !WorkingDays.IsWorkingDay(mask, d.DayOfWeek)) continue;

            list.Add(new AttendanceDay
            {
                Date = d,
                CheckIn = recs?.FirstOrDefault(r => r.AttendanceType == AttendanceConstants.CheckIn),
                CheckOut = recs?.FirstOrDefault(r => r.AttendanceType == AttendanceConstants.CheckOut)
            });
        }
        return list;
    }

    // ---------- Reminders ----------
    public async Task<bool> RescheduleAsync(int userId)
    {
        await _scheduleGate.WaitAsync();
        try
        {
            // Replace (never stack) the regular reminders. Pending "Next 5 min" snoozes are kept.
            await _notifications.CancelCategoryAsync(userId, AttendanceConstants.CategoryCheckIn);
            await _notifications.CancelCategoryAsync(userId, AttendanceConstants.CategoryCheckOut);

            var s = await GetSettingsAsync(userId);
            if (!s.NotificationsEnabled || !s.AttendanceRemindersEnabled) return false;
            if (!await _notifications.AreNotificationsEnabledAsync()) return false;

            var user = await _users.GetByIdAsync(userId);
            var firstName = user?.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "there";

            var now = DateTime.Now;
            var events = new[]
            {
                (Type: AttendanceConstants.CheckIn, Category: AttendanceConstants.CategoryCheckIn, Minutes: s.CheckInMinutes),
                (Type: AttendanceConstants.CheckOut, Category: AttendanceConstants.CategoryCheckOut, Minutes: s.CheckOutMinutes)
            };

            for (var i = 0; i < AttendanceConstants.ScheduleWindowDays; i++)
            {
                var date = now.Date.AddDays(i);
                if (!WorkingDays.IsWorkingDay(s.WorkingDaysMask, date.DayOfWeek)) continue;
                var dateKey = AttendanceConstants.DateKey(date);

                foreach (var e in events)
                {
                    var fireAt = date.AddMinutes(e.Minutes);
                    if (fireAt <= now) continue;
                    if (i == 0 && await _attendance.GetAsync(userId, dateKey, e.Type) is not null) continue;

                    await _notifications.ScheduleAsync(BuildJob(userId, e.Type, e.Category, dateKey, fireAt, firstName));
                }
            }
            return true;
        }
        finally
        {
            _scheduleGate.Release();
        }
    }

    public Task CancelAllAsync(int userId) => _notifications.CancelAllForUserAsync(userId);

    public Task<DateTime?> GetNextReminderAsync(int userId) =>
        _notifications.GetNextFireLocalAsync(userId, AttendanceConstants.CategoryPrefix);

    // ---------- Notification buttons ----------
    public async Task HandleNotificationActionAsync(int actionId, string? payload)
    {
        if (!TryParsePayload(payload, out var userId, out var eventType, out var date)) return;

        switch (actionId)
        {
            case AttendanceConstants.ActionPresent:
                await MarkAsync(userId, eventType, AttendanceConstants.Present, date, overwrite: false,
                    AttendanceConstants.SourceNotification);
                break;

            case AttendanceConstants.ActionAbsent:
                await MarkAsync(userId, eventType, AttendanceConstants.Absent, date, overwrite: false,
                    AttendanceConstants.SourceNotification);
                break;

            case AttendanceConstants.ActionSnooze:
                await SnoozeAsync(userId, eventType, date);
                break;
        }
    }

    /// <summary>"Next 5 Min": nothing is marked; the same alert returns after exactly 5 minutes.</summary>
    private async Task SnoozeAsync(int userId, string eventType, DateTime date)
    {
        var dateKey = AttendanceConstants.DateKey(date);
        if (await _attendance.GetAsync(userId, dateKey, eventType) is not null) return; // already answered

        var user = await _users.GetByIdAsync(userId);
        var firstName = user?.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "there";
        var fireAt = DateTime.Now.AddMinutes(AttendanceConstants.SnoozeMinutes);

        var job = BuildJob(userId, eventType, AttendanceConstants.CategorySnooze, dateKey, fireAt, firstName)
            with { ReferenceKey = $"{dateKey}|{eventType}" };
        await _notifications.ScheduleAsync(job);
    }

    private static NotificationJob BuildJob(int userId, string eventType, string category, string dateKey,
        DateTime fireAt, string firstName)
    {
        var isIn = eventType == AttendanceConstants.CheckIn;
        var title = isIn ? "Check-in time" : "Check-out time";
        var message = isIn
            ? $"Good morning, {firstName}. Have you reached work? Mark your attendance."
            : $"{firstName}, your workday is ending. Mark your check-out.";
        var payload = string.Join('|', AttendanceConstants.PayloadPrefix, userId, eventType, dateKey);
        return new NotificationJob(userId, category, dateKey, fireAt, title, message, payload, NotificationActionSet.Attendance);
    }

    private static bool TryParsePayload(string? payload, out int userId, out string eventType, out DateTime date)
    {
        userId = 0; eventType = string.Empty; date = default;
        var parts = payload?.Split('|');
        return parts is { Length: 4 }
               && parts[0] == AttendanceConstants.PayloadPrefix
               && int.TryParse(parts[1], out userId)
               && AttendanceConstants.IsValidType(eventType = parts[2])
               && DateTime.TryParseExact(parts[3], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                      DateTimeStyles.None, out date);
    }
}
