using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Models;
using MyActivity.Services;

namespace MyActivity.ViewModels;

public partial class FilterOption : ObservableObject
{
    private readonly Action<FilterOption> _onSelected;

    public FilterOption(string key, string title, Action<FilterOption> onSelected)
    {
        Key = key;
        Title = title;
        _onSelected = onSelected;
    }

    public string Key { get; }
    public string Title { get; }

    [ObservableProperty] private bool _isSelected;

    [RelayCommand] private void Select() => _onSelected(this);
}

/// <summary>Presentation row for one day in the history list.</summary>
public class AttendanceDayItem
{
    public string DayNumber { get; init; } = string.Empty;
    public string MonthText { get; init; } = string.Empty;
    public string WeekdayText { get; init; } = string.Empty;
    public string CheckInText { get; init; } = string.Empty;
    public string CheckOutText { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
    public Color StatusColor { get; init; } = Colors.Gray;

    public static AttendanceDayItem From(AttendanceDay day, DateTime today)
    {
        var isToday = day.Date == today;
        var culture = CultureInfo.CurrentCulture;

        static string TimeOrState(Attendance? a, bool isIn) =>
            a is null ? "--:--"
            : a.Status == AttendanceConstants.Present ? AttendanceFormat.Time(isIn ? a.CheckInTime : a.CheckOutTime)
            : "Absent";

        return new AttendanceDayItem
        {
            DayNumber = day.Date.ToString("dd", culture),
            MonthText = day.Date.ToString("MMM", culture),
            WeekdayText = isToday ? "Today" : day.Date.ToString("dddd", culture),
            CheckInText = TimeOrState(day.CheckIn, true),
            CheckOutText = TimeOrState(day.CheckOut, false),
            StatusText = AttendanceFormat.StatusText(day.CheckIn, isToday),
            StatusColor = AttendanceFormat.StatusColor(day.CheckIn)
        };
    }
}

public partial class AttendanceHistoryViewModel : BaseViewModel
{
    private const int MaxRangeDays = 366;

    private readonly IAttendanceService _attendance;
    private readonly ISessionService _session;
    private readonly INavigationService _nav;
    private readonly ILogger<AttendanceHistoryViewModel> _logger;
    private string _filterKey = "today";
    private bool _loading;
    private bool _reloadRequested;

    public AttendanceHistoryViewModel(IAttendanceService attendance, ISessionService session,
        INavigationService nav, ILogger<AttendanceHistoryViewModel> logger)
    {
        _attendance = attendance;
        _session = session;
        _nav = nav;
        _logger = logger;

        Filters = new ObservableCollection<FilterOption>
        {
            new("today", "Today", Select),
            new("week", "This week", Select),
            new("month", "This month", Select),
            new("custom", "Custom", Select)
        };
        Filters[0].IsSelected = true;
        FromDate = DateTime.Today.AddDays(-7);
        ToDate = DateTime.Today;
    }

    public ObservableCollection<FilterOption> Filters { get; }
    public ObservableCollection<AttendanceDayItem> Items { get; } = new();

    [ObservableProperty] private DateTime _fromDate;
    [ObservableProperty] private DateTime _toDate;
    [ObservableProperty] private bool _isCustom;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _summaryText = string.Empty;

    public DateTime MaxDate => DateTime.Today;

    partial void OnFromDateChanged(DateTime value) { if (IsCustom) _ = LoadAsync(); }
    partial void OnToDateChanged(DateTime value) { if (IsCustom) _ = LoadAsync(); }

    private void Select(FilterOption option)
    {
        foreach (var f in Filters) f.IsSelected = ReferenceEquals(f, option);
        _filterKey = option.Key;
        IsCustom = option.Key == "custom";
        _ = LoadAsync();
    }

    private (DateTime From, DateTime To) CurrentRange()
    {
        var today = DateTime.Today;
        return _filterKey switch
        {
            "week" => (today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), today),      // Monday -> today
            "month" => (new DateTime(today.Year, today.Month, 1), today),
            "custom" => (FromDate.Date, ToDate.Date),
            _ => (today, today)
        };
    }

    public async Task LoadAsync()
    {
        if (!_session.IsAuthenticated) return;
        if (_loading) { _reloadRequested = true; return; }   // a filter changed mid-load: run again afterwards
        _loading = true;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var (from, to) = CurrentRange();
            if (from > to) { (from, to) = (to, from); }
            if ((to - from).TotalDays > MaxRangeDays)
            {
                ErrorMessage = "Please choose a range of up to one year.";
                return;
            }

            var days = await _attendance.GetDaysAsync(_session.RequireUserId(), from, to);
            var today = DateTime.Today;

            Items.Clear();
            foreach (var d in days) Items.Add(AttendanceDayItem.From(d, today));

            IsEmpty = Items.Count == 0;
            var present = days.Count(d => d.CheckIn?.Status == AttendanceConstants.Present);
            var absent = days.Count(d => d.CheckIn?.Status == AttendanceConstants.Absent);
            var notMarked = days.Count(d => d.CheckIn is null && d.Date != today);
            SummaryText = $"{present} present   {absent} absent   {notMarked} not marked";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Attendance history load failed");
            ErrorMessage = AppConstants.Messages.LoadFailed;
        }
        finally
        {
            IsBusy = false;
            _loading = false;
        }

        if (_reloadRequested)
        {
            _reloadRequested = false;
            await LoadAsync();
        }
    }

    [RelayCommand] private Task BackAsync() => _nav.GoToAsync(AppConstants.Routes.Back);
}
