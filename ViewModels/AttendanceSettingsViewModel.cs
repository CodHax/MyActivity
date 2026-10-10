using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Services;

namespace MyActivity.ViewModels;

public partial class DayToggleItem : ObservableObject
{
    public DayToggleItem(DayOfWeek day, string label, bool selected)
    {
        Day = day;
        Label = label;
        _isSelected = selected;
    }

    public DayOfWeek Day { get; }
    public string Label { get; }

    [ObservableProperty] private bool _isSelected;

    [RelayCommand] private void Toggle() => IsSelected = !IsSelected;
}

public partial class AttendanceSettingsViewModel : BaseViewModel
{
    private readonly IAttendanceService _attendance;
    private readonly ISessionService _session;
    private readonly INotificationService _notifications;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly ILogger<AttendanceSettingsViewModel> _logger;

    public AttendanceSettingsViewModel(IAttendanceService attendance, ISessionService session,
        INotificationService notifications, INavigationService nav, IDialogService dialogs,
        ILogger<AttendanceSettingsViewModel> logger)
    {
        _attendance = attendance;
        _session = session;
        _notifications = notifications;
        _nav = nav;
        _dialogs = dialogs;
        _logger = logger;
    }

    [ObservableProperty] private TimeSpan _checkInTime = new(8, 30, 0);
    [ObservableProperty] private TimeSpan _checkOutTime = new(17, 30, 0);
    [ObservableProperty] private bool _remindersEnabled = true;

    public ObservableCollection<DayToggleItem> Days { get; } = new();

    public async Task LoadAsync()
    {
        if (!_session.IsAuthenticated) return;
        try
        {
            var s = await _attendance.GetSettingsAsync(_session.RequireUserId());
            CheckInTime = TimeSpan.FromMinutes(s.CheckInMinutes);
            CheckOutTime = TimeSpan.FromMinutes(s.CheckOutMinutes);
            RemindersEnabled = s.AttendanceRemindersEnabled;

            Days.Clear();
            // Monday first
            foreach (var (day, label) in new[]
                     {
                         (DayOfWeek.Monday, "Mo"), (DayOfWeek.Tuesday, "Tu"), (DayOfWeek.Wednesday, "We"),
                         (DayOfWeek.Thursday, "Th"), (DayOfWeek.Friday, "Fr"), (DayOfWeek.Saturday, "Sa"),
                         (DayOfWeek.Sunday, "Su")
                     })
                Days.Add(new DayToggleItem(day, label, WorkingDays.IsWorkingDay(s.WorkingDaysMask, day)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loading attendance settings failed");
            ErrorMessage = AppConstants.Messages.LoadFailed;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            if (RemindersEnabled && !await _notifications.AreNotificationsEnabledAsync())
                await _notifications.RequestPermissionAsync();

            var mask = Days.Aggregate(0, (m, d) => WorkingDays.Set(m, d.Day, d.IsSelected));
            var result = await _attendance.SaveScheduleAsync(_session.RequireUserId(), CheckInTime, CheckOutTime,
                mask, RemindersEnabled);

            if (!result.Success) { ErrorMessage = result.Message; return; }

            await _dialogs.AlertAsync("Saved", result.Message);
            await _nav.GoToAsync(AppConstants.Routes.Back);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving attendance settings failed");
            ErrorMessage = AppConstants.Messages.SaveFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand] private Task BackAsync() => _nav.GoToAsync(AppConstants.Routes.Back);
}
