using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Helpers;

namespace MyActivity.Services;

/// <summary>Receives notification taps/button presses from the plugin and routes them by payload type.</summary>
public interface INotificationActionRouter
{
    void OnActionTapped(int actionId, string? payload, bool isTapped, bool isDismissed);
}

public class NotificationActionRouter : INotificationActionRouter
{
    private readonly IAttendanceService _attendance;
    private readonly ITaskService _tasks;
    private readonly ISessionService _session;
    private readonly INavigationService _nav;
    private readonly ILogger<NotificationActionRouter> _logger;

    public NotificationActionRouter(IAttendanceService attendance, ITaskService tasks, ISessionService session,
        INavigationService nav, ILogger<NotificationActionRouter> logger)
    {
        _attendance = attendance;
        _tasks = tasks;
        _session = session;
        _nav = nav;
        _logger = logger;
    }

    // async void is deliberate: this is an event handler, and every path is wrapped in try/catch.
    public async void OnActionTapped(int actionId, string? payload, bool isTapped, bool isDismissed)
    {
        try
        {
            if (isDismissed || string.IsNullOrEmpty(payload)) return;

            var isAttendance = payload.StartsWith(AttendanceConstants.PayloadPrefix + "|", StringComparison.Ordinal);
            var isMeeting = payload.StartsWith(MeetingConstants.PayloadPrefix + "|", StringComparison.Ordinal);
            var isTask = payload.StartsWith(TaskConstants.PayloadPrefix + "|", StringComparison.Ordinal);
            if (!isAttendance && !isMeeting && !isTask) return;

            if (isTapped)
            {
                // Cold start is handled by StartupViewModel (the session is not restored yet at this point).
                if (!_session.IsAuthenticated) return;
                var route = RouteFor(payload, isAttendance, isMeeting);
                if (route is null || _nav.CurrentLocation.EndsWith(route.TrimStart('/'), StringComparison.OrdinalIgnoreCase)) return;
                await MainThread.InvokeOnMainThreadAsync(() => _nav.GoToAsync(route));
                return;
            }

            if (isAttendance) await _attendance.HandleNotificationActionAsync(actionId, payload);
            else if (isTask) await _tasks.HandleNotificationActionAsync(actionId, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification action failed");
        }
    }

    /// <summary>Where a tapped notification should take the user (also used for cold start).</summary>
    public static string? RouteFor(string payload, bool isAttendance, bool isMeeting)
    {
        if (isAttendance) return AppConstants.Routes.Attendance;
        if (isMeeting && NotificationPayload.TryParse(payload, MeetingConstants.PayloadPrefix, out _, out var id))
            return $"{AppConstants.Routes.MeetingDetail}?id={id}";
        return AppConstants.Routes.Tasks;
    }
}
