using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Services;
using Plugin.LocalNotification;

namespace MyActivity.ViewModels;

public class StartupViewModel : BaseViewModel
{
    private readonly IDatabaseService _database;
    private readonly ISessionService _session;
    private readonly IReminderCoordinator _reminders;
    private readonly IThemeService _theme;
    private readonly INavigationService _nav;
    private readonly ILogger<StartupViewModel> _logger;

    public StartupViewModel(IDatabaseService database, ISessionService session, IReminderCoordinator reminders,
        IThemeService theme, INavigationService nav, ILogger<StartupViewModel> logger)
    {
        _database = database;
        _session = session;
        _reminders = reminders;
        _theme = theme;
        _nav = nav;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _database.GetConnectionAsync();      // create + migrate DB
            var restored = await _session.TryRestoreAsync();

            if (!restored)
            {
                await _nav.GoToAsync(AppConstants.Routes.Login);
                return;
            }

            try
            {
                var userId = _session.RequireUserId();
                await _theme.ApplyForUserAsync(userId);
                await _reminders.RescheduleAllAsync(userId);   // top up the rolling windows
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Startup setup failed"); }

            await _nav.GoToAsync(AppConstants.Routes.Dashboard);

            // App was opened by tapping a notification -> go to the relevant screen.
            var launch = LocalNotificationCenter.LaunchNotificationDetails;
            var data = launch?.Request?.ReturningData;
            if (launch?.DidNotificationLaunchApp == true && !string.IsNullOrEmpty(data))
            {
                var isAtt = data.StartsWith(AttendanceConstants.PayloadPrefix + "|");
                var isMtg = data.StartsWith(MeetingConstants.PayloadPrefix + "|");
                var isTask = data.StartsWith(TaskConstants.PayloadPrefix + "|");
                if (isAtt || isMtg || isTask)
                {
                    var route = NotificationActionRouter.RouteFor(data, isAtt, isMtg);
                    if (route is not null) await _nav.GoToAsync(route);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Startup failed");
            await _nav.GoToAsync(AppConstants.Routes.Login);
        }
    }
}
