using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Messages;
using MyActivity.Models;
using MyActivity.Services;

namespace MyActivity.ViewModels;

public partial class AttendanceViewModel : BaseViewModel, IRecipient<AttendanceChangedMessage>
{
    private readonly IAttendanceService _attendance;
    private readonly ISessionService _session;
    private readonly INotificationService _notifications;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly ILogger<AttendanceViewModel> _logger;
    private AttendanceDay? _today;

    public AttendanceViewModel(IAttendanceService attendance, ISessionService session,
        INotificationService notifications, INavigationService nav, IDialogService dialogs,
        ILogger<AttendanceViewModel> logger)
    {
        _attendance = attendance;
        _session = session;
        _notifications = notifications;
        _nav = nav;
        _dialogs = dialogs;
        _logger = logger;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    [ObservableProperty] private string _todayText = string.Empty;
    [ObservableProperty] private string? _infoMessage;

    [ObservableProperty] private string _checkInSchedule = string.Empty;
    [ObservableProperty] private string _checkInStatus = "Pending";
    [ObservableProperty] private string _checkInDetail = string.Empty;
    [ObservableProperty] private Color _checkInColor = Colors.Gray;

    [ObservableProperty] private string _checkOutSchedule = string.Empty;
    [ObservableProperty] private string _checkOutStatus = "Pending";
    [ObservableProperty] private string _checkOutDetail = string.Empty;
    [ObservableProperty] private Color _checkOutColor = Colors.Gray;

    [ObservableProperty] private bool _notificationsOff;
    [ObservableProperty] private string _nextReminderText = string.Empty;

    public void Receive(AttendanceChangedMessage message) =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());

    public async Task LoadAsync()
    {
        if (!_session.IsAuthenticated) return;
        var userId = _session.RequireUserId();

        try
        {
            TodayText = DateTime.Now.ToString("dddd, d MMMM yyyy");
            var s = await _attendance.GetSettingsAsync(userId);
            _today = await _attendance.GetDayAsync(userId, DateTime.Today);

            CheckInSchedule = $"Scheduled {AttendanceFormat.TimeOfDay(s.CheckInMinutes)}";
            CheckOutSchedule = $"Scheduled {AttendanceFormat.TimeOfDay(s.CheckOutMinutes)}";

            Apply(_today.CheckIn, true, out var inStatus, out var inColor, out var inDetail);
            CheckInStatus = inStatus; CheckInColor = inColor; CheckInDetail = inDetail;
            Apply(_today.CheckOut, false, out var outStatus, out var outColor, out var outDetail);
            CheckOutStatus = outStatus; CheckOutColor = outColor; CheckOutDetail = outDetail;

            NotificationsOff = !await _notifications.AreNotificationsEnabledAsync();
            var next = await _attendance.GetNextReminderAsync(userId);
            NextReminderText = !s.AttendanceRemindersEnabled ? "Reminders are turned off."
                : next is null ? "No reminder scheduled."
                : $"Next reminder: {AttendanceFormat.FormatWhen(next.Value)}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Attendance load failed");
            ErrorMessage = AppConstants.Messages.LoadFailed;
        }
    }

    private static void Apply(Attendance? a, bool isCheckIn, out string status, out Color color, out string detail)
    {
        status = AttendanceFormat.StatusText(a, true);
        color = AttendanceFormat.StatusColor(a);
        detail = a is null ? "Not marked yet"
            : a.Status == AttendanceConstants.Present
                ? $"Marked at {AttendanceFormat.Time(isCheckIn ? a.CheckInTime : a.CheckOutTime)}"
                : $"Marked absent at {AttendanceFormat.Time(a.ResponseTime)}";
    }

    /// <summary>Parameter format: "CheckIn|Present", "CheckOut|Absent", ...</summary>
    [RelayCommand]
    private async Task MarkAsync(string? arg)
    {
        var parts = arg?.Split('|');
        if (parts is not { Length: 2 } || IsBusy) return;
        var (type, status) = (parts[0], parts[1]);
        var label = type == AttendanceConstants.CheckIn ? "check-in" : "check-out";

        var existing = type == AttendanceConstants.CheckIn ? _today?.CheckIn : _today?.CheckOut;
        if (existing is not null)
        {
            if (existing.Status == status) { InfoMessage = $"Your {label} is already {status}."; return; }
            if (!await _dialogs.ConfirmAsync("Change attendance",
                    $"Change today's {label} from {existing.Status} to {status}?", "Change", "Cancel")) return;
        }

        IsBusy = true;
        ErrorMessage = null;
        InfoMessage = null;
        try
        {
            var result = await _attendance.MarkAsync(_session.RequireUserId(), type, status, DateTime.Today,
                overwrite: true, AttendanceConstants.SourceManual);
            if (result.Success) InfoMessage = result.Message; else ErrorMessage = result.Message;
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EnableNotificationsAsync()
    {
        await _notifications.RequestPermissionAsync();
        await _attendance.RescheduleAsync(_session.RequireUserId());
        await LoadAsync();
    }

    [RelayCommand] private Task OpenSettingsAsync() => _nav.GoToAsync(AppConstants.Routes.AttendanceSettings);
    [RelayCommand] private Task OpenHistoryAsync() => _nav.GoToAsync(AppConstants.Routes.AttendanceHistory);
    [RelayCommand] private Task BackAsync() => _nav.GoToAsync(AppConstants.Routes.Back);
}
