using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Services;

namespace MyActivity.ViewModels;

public partial class ToolsViewModel : BaseViewModel
{
    private readonly INavigationService _nav;
    public ToolsViewModel(INavigationService nav) => _nav = nav;

    [RelayCommand] private Task OpenAsync(string? route) =>
        string.IsNullOrEmpty(route) ? Task.CompletedTask : _nav.GoToAsync(route);
}

public partial class MoreViewModel : BaseViewModel
{
    private readonly ISessionService _session;
    private readonly ISettingsService _settings;
    private readonly IThemeService _theme;
    private readonly IReminderCoordinator _reminders;
    private readonly INotificationService _notifications;
    private readonly IAuthenticationService _auth;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly ILogger<MoreViewModel> _logger;
    private bool _loading;

    public MoreViewModel(ISessionService session, ISettingsService settings, IThemeService theme,
        IReminderCoordinator reminders, INotificationService notifications, IAuthenticationService auth,
        INavigationService nav, IDialogService dialogs, ILogger<MoreViewModel> logger)
    {
        _session = session;
        _settings = settings;
        _theme = theme;
        _reminders = reminders;
        _notifications = notifications;
        _auth = auth;
        _nav = nav;
        _dialogs = dialogs;
        _logger = logger;

        Themes = new ObservableCollection<FilterOption>
        {
            new(AppConstants.Themes.System, "System", SelectTheme),
            new(AppConstants.Themes.Light, "Light", SelectTheme),
            new(AppConstants.Themes.Dark, "Dark", SelectTheme)
        };
    }

    public ObservableCollection<FilterOption> Themes { get; }
    public string VersionText => $"Version {AppInfo.Current.VersionString}";

    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _employeeId = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _initials = string.Empty;

    [ObservableProperty] private bool _notificationsEnabled = true;
    [ObservableProperty] private bool _attendanceReminders = true;
    [ObservableProperty] private bool _meetingReminders = true;
    [ObservableProperty] private bool _taskReminders = true;

    partial void OnNotificationsEnabledChanged(bool value) => _ = SaveNotificationSettingsAsync();
    partial void OnAttendanceRemindersChanged(bool value) => _ = SaveNotificationSettingsAsync();
    partial void OnMeetingRemindersChanged(bool value) => _ = SaveNotificationSettingsAsync();
    partial void OnTaskRemindersChanged(bool value) => _ = SaveNotificationSettingsAsync();

    public async Task LoadAsync()
    {
        var user = _session.CurrentUser;
        if (user is null) return;

        _loading = true;
        try
        {
            FullName = user.FullName;
            EmployeeId = user.EmployeeId;
            Email = user.Email;
            Initials = user.Initials;

            var s = await _settings.GetAsync(user.Id);
            NotificationsEnabled = s.NotificationsEnabled;
            AttendanceReminders = s.AttendanceRemindersEnabled;
            MeetingReminders = s.MeetingRemindersEnabled;
            TaskReminders = s.TaskRemindersEnabled;
            foreach (var t in Themes) t.IsSelected = t.Key == s.Theme;
            if (Themes.All(t => !t.IsSelected)) Themes[0].IsSelected = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "More load failed");
            ErrorMessage = AppConstants.Messages.LoadFailed;
        }
        finally
        {
            _loading = false;
        }
    }

    private async void SelectTheme(FilterOption option)
    {
        foreach (var t in Themes) t.IsSelected = ReferenceEquals(t, option);
        if (_loading || !_session.IsAuthenticated) return;
        try
        {
            var s = await _settings.GetAsync(_session.RequireUserId());
            s.Theme = option.Key;
            await _settings.SaveAsync(s);
            _theme.Apply(option.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving theme failed");
            ErrorMessage = AppConstants.Messages.SaveFailed;
        }
    }

    private async Task SaveNotificationSettingsAsync()
    {
        if (_loading || !_session.IsAuthenticated) return;
        try
        {
            var userId = _session.RequireUserId();
            var s = await _settings.GetAsync(userId);
            s.NotificationsEnabled = NotificationsEnabled;
            s.AttendanceRemindersEnabled = AttendanceReminders;
            s.MeetingRemindersEnabled = MeetingReminders;
            s.TaskRemindersEnabled = TaskReminders;
            await _settings.SaveAsync(s);

            if (NotificationsEnabled && !await _notifications.AreNotificationsEnabledAsync())
                await _notifications.RequestPermissionAsync();

            // Disabled categories are skipped by the services; enabled ones are rebuilt.
            await _reminders.CancelAllAsync(userId);
            await _reminders.RescheduleAllAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving notification settings failed");
            ErrorMessage = AppConstants.Messages.SaveFailed;
        }
    }

    [RelayCommand] private Task ChangePasswordAsync() => _nav.GoToAsync(AppConstants.Routes.ChangePassword);

    [RelayCommand]
    private Task AboutAsync() => _dialogs.AlertAsync("My Activity",
        $"{VersionText}\n\nAn offline employee productivity app. All your data stays on this device - no internet or account server is used.");

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (!await _dialogs.ConfirmAsync("Log out", "Do you want to log out of My Activity?", "Log out", "Stay")) return;
        await _auth.LogoutAsync();
        await _nav.GoToAsync(AppConstants.Routes.Login);
    }
}

public partial class ChangePasswordViewModel : BaseViewModel
{
    private readonly IAuthenticationService _auth;
    private readonly ISessionService _session;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;

    public ChangePasswordViewModel(IAuthenticationService auth, ISessionService session,
        INavigationService nav, IDialogService dialogs)
    {
        _auth = auth;
        _session = session;
        _nav = nav;
        _dialogs = dialogs;
    }

    [ObservableProperty] private string _currentPassword = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private string? _currentError;
    [ObservableProperty] private string? _newError;
    [ObservableProperty] private string? _confirmError;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        CurrentError = string.IsNullOrEmpty(CurrentPassword) ? "Please enter your current password." : null;
        NewError = Validators.ValidatePassword(NewPassword);
        ConfirmError = Validators.ValidateConfirmPassword(NewPassword, ConfirmPassword);
        if (CurrentError is not null || NewError is not null || ConfirmError is not null) return;

        IsBusy = true;
        try
        {
            var result = await _auth.ChangePasswordAsync(_session.RequireUserId(), CurrentPassword, NewPassword, ConfirmPassword);
            if (!result.Success) { ErrorMessage = result.Message; return; }

            CurrentPassword = NewPassword = ConfirmPassword = string.Empty;
            await _dialogs.AlertAsync("Password updated", result.Message);
            await _nav.GoToAsync(AppConstants.Routes.Back);
        }
        catch
        {
            ErrorMessage = AppConstants.Messages.SaveFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand] private Task BackAsync() => _nav.GoToAsync(AppConstants.Routes.Back);
}
