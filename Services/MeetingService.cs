using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Messages;
using MyActivity.Models;
using MyActivity.Repositories;

namespace MyActivity.Services;

public record MeetingInput(string? Title, string? Description, DateTime Date, TimeSpan Start, TimeSpan End,
    string? Location, string? Participants, string? Notes, int ReminderMinutes);

public enum ReminderStatus { None, Scheduled, TimePassed, Disabled }

public interface IMeetingService
{
    Task<ServiceResult<Meeting>> SaveAsync(int userId, int? id, MeetingInput input);
    Task<ServiceResult> DeleteAsync(int userId, int id);
    Task<Meeting?> GetAsync(int userId, int id);
    Task<List<Meeting>> ListAsync(int userId, MeetingScope scope);
    Task<Meeting?> GetNextAsync(int userId);
    Task RescheduleAllAsync(int userId);
}

public class MeetingService : IMeetingService
{
    private const int ReminderWindowDays = 60;

    private readonly IMeetingRepository _repo;
    private readonly ISettingsService _settings;
    private readonly INotificationService _notifications;
    private readonly ILogger<MeetingService> _logger;

    public MeetingService(IMeetingRepository repo, ISettingsService settings,
        INotificationService notifications, ILogger<MeetingService> logger)
    {
        _repo = repo;
        _settings = settings;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<ServiceResult<Meeting>> SaveAsync(int userId, int? id, MeetingInput i)
    {
        var error = Validate(i);
        if (error is not null) return ServiceResult<Meeting>.Fail(error);

        try
        {
            var now = DateTime.UtcNow;
            Meeting m;
            if (id is null) m = new Meeting { UserId = userId, CreatedAt = now };
            else
            {
                var existing = await _repo.GetAsync(userId, id.Value);
                if (existing is null) return ServiceResult<Meeting>.Fail("This meeting no longer exists.");
                m = existing;
            }

            m.Title = i.Title!.Trim();
            m.Description = Clean(i.Description);
            m.MeetingDate = AttendanceConstants.DateKey(i.Date);
            m.StartTime = Hm(i.Start);
            m.EndTime = Hm(i.End);
            m.Location = Clean(i.Location);
            m.Participants = Clean(i.Participants);
            m.Notes = Clean(i.Notes);
            m.ReminderMinutes = i.ReminderMinutes;
            m.UpdatedAt = now;

            if (id is null) await _repo.InsertAsync(m); else await _repo.UpdateAsync(m);

            var (status, fire) = await ScheduleAsync(userId, m);
            WeakReferenceMessenger.Default.Send(new MeetingsChangedMessage());

            var message = "Meeting saved successfully." + status switch
            {
                ReminderStatus.Scheduled => $" Reminder set for {AttendanceFormat.FormatWhen(fire!.Value)}.",
                ReminderStatus.TimePassed => " The reminder time has already passed, so no reminder was set.",
                ReminderStatus.Disabled => " Turn on notifications to receive meeting reminders.",
                _ => string.Empty
            };
            return ServiceResult<Meeting>.Ok(m, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving meeting failed");
            return ServiceResult<Meeting>.Fail(AppConstants.Messages.SaveFailed);
        }
    }

    public async Task<ServiceResult> DeleteAsync(int userId, int id)
    {
        try
        {
            await _notifications.CancelAsync(userId, MeetingConstants.CategoryReminder, id.ToString());
            if (!await _repo.DeleteAsync(userId, id)) return ServiceResult.Fail("This meeting no longer exists.");
            WeakReferenceMessenger.Default.Send(new MeetingsChangedMessage());
            return ServiceResult.Ok("Meeting deleted.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deleting meeting failed");
            return ServiceResult.Fail(AppConstants.Messages.SaveFailed);
        }
    }

    public Task<Meeting?> GetAsync(int userId, int id) => _repo.GetAsync(userId, id);

    public Task<List<Meeting>> ListAsync(int userId, MeetingScope scope) =>
        _repo.QueryAsync(userId, scope, AttendanceConstants.DateKey(DateTime.Today));

    public Task<Meeting?> GetNextAsync(int userId) =>
        _repo.GetNextAsync(userId, DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Rebuilds every upcoming meeting reminder (after login, app start or a settings change).</summary>
    public async Task RescheduleAllAsync(int userId)
    {
        await _notifications.CancelCategoryAsync(userId, MeetingConstants.CategoryPrefix);

        var limit = DateTime.Today.AddDays(ReminderWindowDays);
        var meetings = await _repo.GetFromDateAsync(userId, AttendanceConstants.DateKey(DateTime.Today));
        foreach (var m in meetings.Where(x => x.ReminderMinutes >= 0 && x.StartLocal <= limit))
            await ScheduleAsync(userId, m);
    }

    private async Task<(ReminderStatus Status, DateTime? Fire)> ScheduleAsync(int userId, Meeting m)
    {
        await _notifications.CancelAsync(userId, MeetingConstants.CategoryReminder, m.Id.ToString());
        if (m.ReminderMinutes < 0) return (ReminderStatus.None, null);

        var fire = m.StartLocal.AddMinutes(-m.ReminderMinutes);
        if (fire <= DateTime.Now) return (ReminderStatus.TimePassed, null);

        var s = await _settings.GetAsync(userId);
        if (!s.NotificationsEnabled || !s.MeetingRemindersEnabled || !await _notifications.AreNotificationsEnabledAsync())
            return (ReminderStatus.Disabled, null);

        var message = m.ReminderMinutes == 0
            ? $"{m.Title} is starting now."
            : $"{m.Title} starts in {MeetingConstants.DescribeLead(m.ReminderMinutes)}.";

        var scheduled = await _notifications.ScheduleAsync(new NotificationJob(
            userId, MeetingConstants.CategoryReminder, m.Id.ToString(), fire,
            "Meeting Reminder", message, NotificationPayload.Build(MeetingConstants.PayloadPrefix, userId, m.Id)));

        return scheduled is null ? (ReminderStatus.Disabled, null) : (ReminderStatus.Scheduled, fire);
    }

    private static string? Validate(MeetingInput i)
    {
        if (string.IsNullOrWhiteSpace(i.Title)) return "Please enter a meeting title.";
        if (i.Title.Trim().Length > 120) return "Title must be 120 characters or fewer.";
        if (i.End <= i.Start) return "End time must be later than start time.";
        if (i.Description is { Length: > 2000 } || i.Notes is { Length: > 2000 }) return "Description and notes must be 2000 characters or fewer.";
        if (i.Location is { Length: > 200 }) return "Location must be 200 characters or fewer.";
        if (i.Participants is { Length: > 500 }) return "Participants must be 500 characters or fewer.";
        if (!MeetingConstants.ReminderOptions.Any(o => o.Minutes == i.ReminderMinutes)) return "Please choose a valid reminder option.";
        return null;
    }

    private static string Hm(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}";
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
