using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Messages;
using MyActivity.Repositories;
using MyActivity.Services;

namespace MyActivity.ViewModels;

public partial class MeetingsViewModel : BaseViewModel, IRecipient<MeetingsChangedMessage>
{
    private readonly IMeetingService _meetings;
    private readonly ISessionService _session;
    private readonly INavigationService _nav;
    private readonly ILogger<MeetingsViewModel> _logger;
    private MeetingScope _scope = MeetingScope.Today;
    private bool _loading, _reloadRequested;

    public MeetingsViewModel(IMeetingService meetings, ISessionService session, INavigationService nav,
        ILogger<MeetingsViewModel> logger)
    {
        _meetings = meetings;
        _session = session;
        _nav = nav;
        _logger = logger;

        Filters = new ObservableCollection<FilterOption>
        {
            new("today", "Today", Select), new("upcoming", "Upcoming", Select), new("history", "History", Select)
        };
        Filters[0].IsSelected = true;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ObservableCollection<FilterOption> Filters { get; }
    public ObservableCollection<MeetingItem> Items { get; } = new();

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _emptyTitle = string.Empty;
    [ObservableProperty] private string _emptyHint = string.Empty;

    private void Select(FilterOption o)
    {
        foreach (var f in Filters) f.IsSelected = ReferenceEquals(f, o);
        _scope = o.Key switch { "upcoming" => MeetingScope.Upcoming, "history" => MeetingScope.History, _ => MeetingScope.Today };
        _ = LoadAsync();
    }

    public void Receive(MeetingsChangedMessage message) =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());

    public async Task LoadAsync()
    {
        if (!_session.IsAuthenticated) return;
        if (_loading) { _reloadRequested = true; return; }
        _loading = true;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var rows = await _meetings.ListAsync(_session.RequireUserId(), _scope);
            Items.Clear();
            foreach (var m in rows) Items.Add(new MeetingItem(m, Open));
            IsEmpty = Items.Count == 0;
            (EmptyTitle, EmptyHint) = _scope switch
            {
                MeetingScope.Today => ("No meetings today", "Tap + to schedule one."),
                MeetingScope.Upcoming => ("No upcoming meetings", "Tap + to schedule one."),
                _ => ("No past meetings", "Meetings from earlier days appear here.")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Meetings load failed");
            ErrorMessage = AppConstants.Messages.LoadFailed;
        }
        finally
        {
            IsBusy = false;
            _loading = false;
        }
        if (_reloadRequested) { _reloadRequested = false; await LoadAsync(); }
    }

    private async void Open(MeetingItem item) =>
        await _nav.GoToAsync($"{AppConstants.Routes.MeetingDetail}?id={item.Id}");

    [RelayCommand] private Task AddAsync() => _nav.GoToAsync(AppConstants.Routes.MeetingEdit);
    [RelayCommand] private Task BackAsync() => _nav.GoToAsync(AppConstants.Routes.Back);
}

public partial class MeetingEditViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IMeetingService _meetings;
    private readonly ISessionService _session;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly ILogger<MeetingEditViewModel> _logger;
    private int? _editId;
    private bool _loaded;

    public MeetingEditViewModel(IMeetingService meetings, ISessionService session, INavigationService nav,
        IDialogService dialogs, INotificationService notifications, ILogger<MeetingEditViewModel> logger)
    {
        _meetings = meetings;
        _session = session;
        _nav = nav;
        _dialogs = dialogs;
        _notifications = notifications;
        _logger = logger;

        var next = DateTime.Now.AddHours(1);
        _date = next.Date;
        _startTime = new TimeSpan(next.Hour, 0, 0);
        _endTime = _startTime + TimeSpan.FromHours(1) < TimeSpan.FromHours(24) ? _startTime + TimeSpan.FromHours(1) : new TimeSpan(23, 59, 0);
        _reminderIndex = Array.FindIndex(MeetingConstants.ReminderOptions, o => o.Minutes == 10);
    }

    public IReadOnlyList<string> ReminderTexts { get; } = MeetingConstants.ReminderOptions.Select(o => o.Text).ToList();

    [ObservableProperty] private string _screenTitle = "New meeting";
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private DateTime _date;
    [ObservableProperty] private TimeSpan _startTime;
    [ObservableProperty] private TimeSpan _endTime;
    [ObservableProperty] private string _location = string.Empty;
    [ObservableProperty] private string _participants = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private int _reminderIndex;
    [ObservableProperty] private string? _titleError;

    partial void OnStartTimeChanged(TimeSpan value)
    {
        if (EndTime <= value) EndTime = value + TimeSpan.FromHours(1) < TimeSpan.FromHours(24) ? value + TimeSpan.FromHours(1) : new TimeSpan(23, 59, 0);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var raw) && int.TryParse(raw?.ToString(), out var id))
        {
            _editId = id;
            ScreenTitle = "Edit meeting";
        }
    }

    public async Task LoadAsync()
    {
        if (_loaded || _editId is null || !_session.IsAuthenticated) return;
        _loaded = true;
        try
        {
            var m = await _meetings.GetAsync(_session.RequireUserId(), _editId.Value);
            if (m is null)
            {
                await _dialogs.AlertAsync("Not found", "This meeting no longer exists.");
                await _nav.GoToAsync(AppConstants.Routes.Back);
                return;
            }
            Title = m.Title;
            Description = m.Description ?? string.Empty;
            Date = m.StartLocal.Date;
            StartTime = m.StartLocal.TimeOfDay;
            EndTime = m.EndLocal.TimeOfDay;
            Location = m.Location ?? string.Empty;
            Participants = m.Participants ?? string.Empty;
            Notes = m.Notes ?? string.Empty;
            ReminderIndex = Math.Max(0, Array.FindIndex(MeetingConstants.ReminderOptions, o => o.Minutes == m.ReminderMinutes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loading meeting failed");
            ErrorMessage = AppConstants.Messages.LoadFailed;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        TitleError = string.IsNullOrWhiteSpace(Title) ? "Please enter a meeting title." : null;
        if (TitleError is not null) return;

        IsBusy = true;
        try
        {
            var minutes = MeetingConstants.ReminderOptions[Math.Clamp(ReminderIndex, 0, MeetingConstants.ReminderOptions.Length - 1)].Minutes;

            // Ask for permission the first time a reminder is actually wanted.
            if (minutes >= 0 && !await _notifications.AreNotificationsEnabledAsync())
                await _notifications.RequestPermissionAsync();

            var result = await _meetings.SaveAsync(_session.RequireUserId(), _editId,
                new MeetingInput(Title, Description, Date, StartTime, EndTime, Location, Participants, Notes, minutes));

            if (!result.Success) { ErrorMessage = result.Message; return; }

            await _dialogs.AlertAsync("Saved", result.Message);
            await _nav.GoToAsync(AppConstants.Routes.Back);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving meeting failed");
            ErrorMessage = AppConstants.Messages.SaveFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand] private Task BackAsync() => _nav.GoToAsync(AppConstants.Routes.Back);
}

public partial class MeetingDetailViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IMeetingService _meetings;
    private readonly ISessionService _session;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly ILogger<MeetingDetailViewModel> _logger;
    private int _id;

    public MeetingDetailViewModel(IMeetingService meetings, ISessionService session, INavigationService nav,
        IDialogService dialogs, ILogger<MeetingDetailViewModel> logger)
    {
        _meetings = meetings;
        _session = session;
        _nav = nav;
        _dialogs = dialogs;
        _logger = logger;
    }

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _dateText = string.Empty;
    [ObservableProperty] private string _timeText = string.Empty;
    [ObservableProperty] private string _reminderText = string.Empty;
    [ObservableProperty] private string _location = string.Empty;
    [ObservableProperty] private string _participants = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    public bool HasLocation => !string.IsNullOrWhiteSpace(Location);
    public bool HasParticipants => !string.IsNullOrWhiteSpace(Participants);
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var raw) && int.TryParse(raw?.ToString(), out var id)) _id = id;
    }

    public async Task LoadAsync()
    {
        if (!_session.IsAuthenticated || _id == 0) return;
        try
        {
            var m = await _meetings.GetAsync(_session.RequireUserId(), _id);
            if (m is null)
            {
                await _nav.GoToAsync(AppConstants.Routes.Back);
                return;
            }
            Title = m.Title;
            DateText = m.StartLocal.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture);
            TimeText = $"{m.StartLocal.ToString("hh:mm tt", CultureInfo.CurrentCulture)} - {m.EndLocal.ToString("hh:mm tt", CultureInfo.CurrentCulture)}";
            ReminderText = MeetingConstants.ReminderOptions.First(o => o.Minutes == m.ReminderMinutes).Text;
            Location = m.Location ?? string.Empty;
            Participants = m.Participants ?? string.Empty;
            Description = m.Description ?? string.Empty;
            Notes = m.Notes ?? string.Empty;
            OnPropertyChanged(nameof(HasLocation));
            OnPropertyChanged(nameof(HasParticipants));
            OnPropertyChanged(nameof(HasDescription));
            OnPropertyChanged(nameof(HasNotes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loading meeting details failed");
            ErrorMessage = AppConstants.Messages.LoadFailed;
        }
    }

    [RelayCommand] private Task EditAsync() => _nav.GoToAsync($"{AppConstants.Routes.MeetingEdit}?id={_id}");

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!await _dialogs.ConfirmAsync("Delete meeting", $"Delete \"{Title}\"? Its reminder will be removed too.",
                "Delete", "Cancel")) return;

        var result = await _meetings.DeleteAsync(_session.RequireUserId(), _id);
        if (!result.Success) { ErrorMessage = result.Message; return; }
        await _nav.GoToAsync(AppConstants.Routes.Back);
    }

    [RelayCommand] private Task BackAsync() => _nav.GoToAsync(AppConstants.Routes.Back);
}
