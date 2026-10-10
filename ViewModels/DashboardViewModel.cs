using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Messages;
using MyActivity.Services;

namespace MyActivity.ViewModels;

public partial class DashboardViewModel : BaseViewModel,
    IRecipient<AttendanceChangedMessage>, IRecipient<TransactionsChangedMessage>,
    IRecipient<MeetingsChangedMessage>, IRecipient<TasksChangedMessage>
{
    private readonly ISessionService _session;
    private readonly IAttendanceService _attendance;
    private readonly IAccountService _account;
    private readonly IMeetingService _meetings;
    private readonly ITaskService _tasks;
    private readonly INotificationService _notifications;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly ILogger<DashboardViewModel> _logger;

    public DashboardViewModel(ISessionService session, IAttendanceService attendance, IAccountService account,
        IMeetingService meetings, ITaskService tasks, INotificationService notifications,
        INavigationService nav, IDialogService dialogs, ILogger<DashboardViewModel> logger)
    {
        _session = session;
        _attendance = attendance;
        _account = account;
        _meetings = meetings;
        _tasks = tasks;
        _notifications = notifications;
        _nav = nav;
        _dialogs = dialogs;
        _logger = logger;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    [ObservableProperty] private string _employeeId = string.Empty;
    [ObservableProperty] private string _initials = string.Empty;
    [ObservableProperty] private string _greeting = string.Empty;
    [ObservableProperty] private string _todayText = string.Empty;

    [ObservableProperty] private string _checkInDetail = string.Empty;
    [ObservableProperty] private string _checkInStatus = "Pending";
    [ObservableProperty] private Color _checkInColor = Palette.Neutral;
    [ObservableProperty] private string _checkOutDetail = string.Empty;
    [ObservableProperty] private string _checkOutStatus = "Pending";
    [ObservableProperty] private Color _checkOutColor = Palette.Neutral;

    [ObservableProperty] private bool _notificationsOff;

    [ObservableProperty] private string _creditText = MoneyFormat.Format(0);
    [ObservableProperty] private string _debitText = MoneyFormat.Format(0);
    [ObservableProperty] private string _balanceText = MoneyFormat.Format(0);
    [ObservableProperty] private Color _balanceColor = Palette.Success;
    [ObservableProperty] private string _upcomingMeetingText = "No upcoming meetings";
    [ObservableProperty] private string _upcomingTaskText = "No upcoming tasks";

    public void Receive(AttendanceChangedMessage message) => Refresh();
    public void Receive(TransactionsChangedMessage message) => Refresh();
    public void Receive(MeetingsChangedMessage message) => Refresh();
    public void Receive(TasksChangedMessage message) => Refresh();

    private void Refresh() => MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());

    public async Task LoadAsync()
    {
        var user = _session.CurrentUser;
        if (user is null) return;

        try
        {
            EmployeeId = user.EmployeeId;
            Initials = user.Initials;
            var first = user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? user.FullName;
            Greeting = $"{GreetingForNow()}, {first}";
            TodayText = DateTime.Now.ToString("dddd, d MMMM yyyy");

            await AskNotificationPermissionOnceAsync(user.Id);
            NotificationsOff = !await _notifications.AreNotificationsEnabledAsync();

            // Attendance
            var settings = await _attendance.GetSettingsAsync(user.Id);
            var day = await _attendance.GetDayAsync(user.Id, DateTime.Today);
            CheckInStatus = AttendanceFormat.StatusText(day.CheckIn, true);
            CheckInColor = AttendanceFormat.StatusColor(day.CheckIn);
            CheckInDetail = day.CheckIn?.Status == AttendanceConstants.Present
                ? $"Marked at {AttendanceFormat.Time(day.CheckIn.CheckInTime)}"
                : $"Scheduled {AttendanceFormat.TimeOfDay(settings.CheckInMinutes)}";
            CheckOutStatus = AttendanceFormat.StatusText(day.CheckOut, true);
            CheckOutColor = AttendanceFormat.StatusColor(day.CheckOut);
            CheckOutDetail = day.CheckOut?.Status == AttendanceConstants.Present
                ? $"Marked at {AttendanceFormat.Time(day.CheckOut.CheckOutTime)}"
                : $"Scheduled {AttendanceFormat.TimeOfDay(settings.CheckOutMinutes)}";

            // Account
            var summary = await _account.GetSummaryAsync(user.Id);
            CreditText = MoneyFormat.Format(summary.Credit);
            DebitText = MoneyFormat.Format(summary.Debit);
            BalanceText = MoneyFormat.Format(summary.Balance);
            BalanceColor = summary.Balance < 0 ? Palette.Danger : Palette.Success;

            // Upcoming meeting / task
            var meeting = await _meetings.GetNextAsync(user.Id);
            UpcomingMeetingText = meeting is null
                ? "No upcoming meetings"
                : $"{meeting.Title} - {AttendanceFormat.FormatWhen(meeting.StartLocal)}";

            var task = await _tasks.GetNextAsync(user.Id);
            UpcomingTaskText = task is null ? "No upcoming tasks"
                : task.ReminderLocal is { } r ? $"{task.Title} - {AttendanceFormat.FormatWhen(r)}"
                : $"{task.Title} - due {DateTime.ParseExact(task.TaskDate!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture):d MMM}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard load failed");
            ErrorMessage = AppConstants.Messages.LoadFailed;
        }
    }

    private async Task AskNotificationPermissionOnceAsync(int userId)
    {
        if (Preferences.Default.Get(AppConstants.NotificationPromptedKey, false)) return;
        Preferences.Default.Set(AppConstants.NotificationPromptedKey, true);

        var allow = await _dialogs.ConfirmAsync("Allow reminders?",
            "My Activity uses notifications for attendance, meeting and task reminders. They work without internet.",
            "Allow", "Not now");
        if (!allow) return;

        await _notifications.RequestPermissionAsync();
        await _attendance.RescheduleAsync(userId);
    }

    private static string GreetingForNow() => DateTime.Now.Hour switch
    {
        < 12 => "Good morning",
        < 17 => "Good afternoon",
        _ => "Good evening"
    };

    [RelayCommand]
    private async Task EnableNotificationsAsync()
    {
        await _notifications.RequestPermissionAsync();
        NotificationsOff = !await _notifications.AreNotificationsEnabledAsync();
        if (!NotificationsOff && _session.CurrentUser is { } user) await _attendance.RescheduleAsync(user.Id);
    }

    [RelayCommand]
    private Task OpenModuleAsync(string? key) => key switch
    {
        "attendance" => _nav.GoToAsync(AppConstants.Routes.Attendance),
        "account" => _nav.GoToAsync(AppConstants.Routes.Account),
        "calculators" => _nav.GoToAsync(AppConstants.Routes.Tools),
        "meetings" => _nav.GoToAsync(AppConstants.Routes.Meetings),
        "tasks" => _nav.GoToAsync(AppConstants.Routes.Tasks),
        _ => Task.CompletedTask
    };
}
