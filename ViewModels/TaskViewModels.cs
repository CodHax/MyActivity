using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Messages;
using MyActivity.Services;

namespace MyActivity.ViewModels;

public partial class TasksViewModel : BaseViewModel, IRecipient<TasksChangedMessage>
{
    private readonly ITaskService _tasks;
    private readonly ISessionService _session;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly ILogger<TasksViewModel> _logger;
    private string? _status = TaskConstants.Pending;
    private bool _loading, _reloadRequested;

    public TasksViewModel(ITaskService tasks, ISessionService session, INavigationService nav,
        IDialogService dialogs, ILogger<TasksViewModel> logger)
    {
        _tasks = tasks;
        _session = session;
        _nav = nav;
        _dialogs = dialogs;
        _logger = logger;

        Filters = new ObservableCollection<FilterOption>
        {
            new(TaskConstants.Pending, "Pending", Select),
            new(TaskConstants.Completed, "Completed", Select),
            new("all", "All", Select)
        };
        Filters[0].IsSelected = true;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ObservableCollection<FilterOption> Filters { get; }
    public ObservableCollection<TaskRowItem> Items { get; } = new();

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _emptyTitle = string.Empty;
    [ObservableProperty] private string? _infoMessage;

    private void Select(FilterOption o)
    {
        foreach (var f in Filters) f.IsSelected = ReferenceEquals(f, o);
        _status = o.Key == "all" ? null : o.Key;
        _ = LoadAsync();
    }

    public void Receive(TasksChangedMessage message) =>
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
            var rows = await _tasks.ListAsync(_session.RequireUserId(), _status);
            Items.Clear();
            foreach (var t in rows) Items.Add(new TaskRowItem(t, Toggle, Edit, Delete));
            IsEmpty = Items.Count == 0;
            EmptyTitle = _status switch
            {
                TaskConstants.Pending => "No pending tasks",
                TaskConstants.Completed => "No completed tasks yet",
                _ => "No tasks or notes yet"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tasks load failed");
            ErrorMessage = AppConstants.Messages.LoadFailed;
        }
        finally
        {
            IsBusy = false;
            _loading = false;
        }
        if (_reloadRequested) { _reloadRequested = false; await LoadAsync(); }
    }

    private async void Toggle(TaskRowItem item)
    {
        var result = await _tasks.SetCompletedAsync(_session.RequireUserId(), item.Id, !item.IsCompleted);
        if (result.Success) InfoMessage = result.Message; else ErrorMessage = result.Message;
    }

    private async void Edit(TaskRowItem item) =>
        await _nav.GoToAsync($"{AppConstants.Routes.TaskEdit}?id={item.Id}");

    private async void Delete(TaskRowItem item)
    {
        if (!await _dialogs.ConfirmAsync("Delete task", $"Delete \"{item.Title}\"? Its reminder will be removed too.",
                "Delete", "Cancel")) return;

        var result = await _tasks.DeleteAsync(_session.RequireUserId(), item.Id);
        if (result.Success) InfoMessage = result.Message; else ErrorMessage = result.Message;
    }

    [RelayCommand] private Task AddAsync() => _nav.GoToAsync(AppConstants.Routes.TaskEdit);
    [RelayCommand] private Task BackAsync() => _nav.GoToAsync(AppConstants.Routes.Back);
}

public partial class TaskEditViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ITaskService _tasks;
    private readonly ISessionService _session;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly ILogger<TaskEditViewModel> _logger;
    private int? _editId;
    private bool _loaded;

    public TaskEditViewModel(ITaskService tasks, ISessionService session, INavigationService nav,
        IDialogService dialogs, INotificationService notifications, ILogger<TaskEditViewModel> logger)
    {
        _tasks = tasks;
        _session = session;
        _nav = nav;
        _dialogs = dialogs;
        _notifications = notifications;
        _logger = logger;

        Priorities = new ObservableCollection<FilterOption>
        {
            new(TaskConstants.Low, "Low", SelectPriority),
            new(TaskConstants.Medium, "Medium", SelectPriority),
            new(TaskConstants.High, "High", SelectPriority)
        };
        Priorities[1].IsSelected = true;

        Statuses = new ObservableCollection<FilterOption>
        {
            new(TaskConstants.Pending, "Pending", SelectStatus),
            new(TaskConstants.Completed, "Completed", SelectStatus)
        };
        Statuses[0].IsSelected = true;

        var next = DateTime.Now.AddHours(1);
        _date = DateTime.Today;
        _reminderDate = next.Date;
        _reminderTime = new TimeSpan(next.Hour, 0, 0);
    }

    public ObservableCollection<FilterOption> Priorities { get; }
    public ObservableCollection<FilterOption> Statuses { get; }

    [ObservableProperty] private string _screenTitle = "New task";
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool _hasDate;
    [ObservableProperty] private DateTime _date;
    [ObservableProperty] private bool _hasReminder;
    [ObservableProperty] private DateTime _reminderDate;
    [ObservableProperty] private TimeSpan _reminderTime;
    [ObservableProperty] private string? _titleError;

    private string SelectedPriority => Priorities.First(p => p.IsSelected).Key;
    private string SelectedStatus => Statuses.First(s => s.IsSelected).Key;

    private void SelectPriority(FilterOption o) { foreach (var p in Priorities) p.IsSelected = ReferenceEquals(p, o); }
    private void SelectStatus(FilterOption o) { foreach (var s in Statuses) s.IsSelected = ReferenceEquals(s, o); }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var raw) && int.TryParse(raw?.ToString(), out var id))
        {
            _editId = id;
            IsEditing = true;
            ScreenTitle = "Edit task";
        }
    }

    public async Task LoadAsync()
    {
        if (_loaded || _editId is null || !_session.IsAuthenticated) return;
        _loaded = true;
        try
        {
            var t = await _tasks.GetAsync(_session.RequireUserId(), _editId.Value);
            if (t is null)
            {
                await _dialogs.AlertAsync("Not found", "This task no longer exists.");
                await _nav.GoToAsync(AppConstants.Routes.Back);
                return;
            }

            Title = t.Title;
            Description = t.Description ?? string.Empty;
            SelectPriority(Priorities.First(p => p.Key == t.Priority));
            SelectStatus(Statuses.First(s => s.Key == t.Status));

            HasDate = t.TaskDate is not null;
            if (t.TaskDate is not null)
                Date = DateTime.ParseExact(t.TaskDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

            HasReminder = t.ReminderLocal is not null;
            if (t.ReminderLocal is { } r)
            {
                ReminderDate = r.Date;
                ReminderTime = r.TimeOfDay;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loading task failed");
            ErrorMessage = AppConstants.Messages.LoadFailed;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        TitleError = string.IsNullOrWhiteSpace(Title) ? "Please enter a task title." : null;
        if (TitleError is not null) return;

        IsBusy = true;
        try
        {
            DateTime? reminder = HasReminder ? ReminderDate.Date + ReminderTime : null;
            if (reminder is not null && SelectedStatus == TaskConstants.Pending &&
                !await _notifications.AreNotificationsEnabledAsync())
                await _notifications.RequestPermissionAsync();

            var result = await _tasks.SaveAsync(_session.RequireUserId(), _editId,
                new TaskInput(Title, Description, HasDate ? Date : null, reminder, SelectedPriority, SelectedStatus));

            if (!result.Success) { ErrorMessage = result.Message; return; }

            await _dialogs.AlertAsync("Saved", result.Message);
            await _nav.GoToAsync(AppConstants.Routes.Back);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving task failed");
            ErrorMessage = AppConstants.Messages.SaveFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand] private Task BackAsync() => _nav.GoToAsync(AppConstants.Routes.Back);
}
