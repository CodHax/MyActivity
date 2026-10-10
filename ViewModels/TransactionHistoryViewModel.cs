using System.Collections.ObjectModel;
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

public partial class TransactionHistoryViewModel : BaseViewModel, IRecipient<TransactionsChangedMessage>
{
    private const string AllCategories = "All categories";

    private readonly IAccountService _account;
    private readonly ISessionService _session;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly ILogger<TransactionHistoryViewModel> _logger;
    private string _typeKey = "all";
    private string _dateKey = "all";
    private bool _loading, _reloadRequested;

    public TransactionHistoryViewModel(IAccountService account, ISessionService session, INavigationService nav,
        IDialogService dialogs, ILogger<TransactionHistoryViewModel> logger)
    {
        _account = account;
        _session = session;
        _nav = nav;
        _dialogs = dialogs;
        _logger = logger;

        TypeFilters = new ObservableCollection<FilterOption>
        {
            new("all", "All", SelectType), new("Credit", "Credit", SelectType), new("Debit", "Debit", SelectType)
        };
        DateFilters = new ObservableCollection<FilterOption>
        {
            new("all", "All time", SelectDate), new("month", "This month", SelectDate), new("custom", "Custom", SelectDate)
        };
        TypeFilters[0].IsSelected = true;
        DateFilters[0].IsSelected = true;

        CategoryOptions = new[] { AllCategories }.Concat(AccountConstants.Categories).ToList();
        _selectedCategory = AllCategories;
        _fromDate = DateTime.Today.AddMonths(-1);
        _toDate = DateTime.Today;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ObservableCollection<FilterOption> TypeFilters { get; }
    public ObservableCollection<FilterOption> DateFilters { get; }
    public ObservableCollection<TransactionItem> Items { get; } = new();
    public IReadOnlyList<string> CategoryOptions { get; }

    [ObservableProperty] private string _selectedCategory;
    [ObservableProperty] private DateTime _fromDate;
    [ObservableProperty] private DateTime _toDate;
    [ObservableProperty] private bool _isCustomDate;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _creditText = string.Empty;
    [ObservableProperty] private string _debitText = string.Empty;
    [ObservableProperty] private string _balanceText = string.Empty;
    [ObservableProperty] private string _countText = string.Empty;

    public DateTime MaxDate => DateTime.Today;

    partial void OnSelectedCategoryChanged(string value) => _ = LoadAsync();
    partial void OnFromDateChanged(DateTime value) { if (IsCustomDate) _ = LoadAsync(); }
    partial void OnToDateChanged(DateTime value) { if (IsCustomDate) _ = LoadAsync(); }

    private void SelectType(FilterOption o)
    {
        foreach (var f in TypeFilters) f.IsSelected = ReferenceEquals(f, o);
        _typeKey = o.Key;
        _ = LoadAsync();
    }

    private void SelectDate(FilterOption o)
    {
        foreach (var f in DateFilters) f.IsSelected = ReferenceEquals(f, o);
        _dateKey = o.Key;
        IsCustomDate = o.Key == "custom";
        _ = LoadAsync();
    }

    public void Receive(TransactionsChangedMessage message) =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());

    private TransactionFilter BuildFilter()
    {
        string? from = null, to = null;
        var today = DateTime.Today;
        if (_dateKey == "month") { from = AttendanceConstants.DateKey(new DateTime(today.Year, today.Month, 1)); to = AttendanceConstants.DateKey(today); }
        else if (_dateKey == "custom")
        {
            var (a, b) = FromDate <= ToDate ? (FromDate, ToDate) : (ToDate, FromDate);
            from = AttendanceConstants.DateKey(a);
            to = AttendanceConstants.DateKey(b);
        }
        return new TransactionFilter(from, to,
            _typeKey == "all" ? null : _typeKey,
            SelectedCategory == AllCategories ? null : SelectedCategory);
    }

    public async Task LoadAsync()
    {
        if (!_session.IsAuthenticated) return;
        if (_loading) { _reloadRequested = true; return; }
        _loading = true;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var userId = _session.RequireUserId();
            var filter = BuildFilter();
            var rows = await _account.QueryAsync(userId, filter);
            var summary = await _account.GetSummaryAsync(userId, filter);

            Items.Clear();
            foreach (var t in rows) Items.Add(new TransactionItem(t, Edit, Delete));
            IsEmpty = Items.Count == 0;

            CreditText = MoneyFormat.Format(summary.Credit);
            DebitText = MoneyFormat.Format(summary.Debit);
            BalanceText = MoneyFormat.Format(summary.Balance);
            CountText = $"{Items.Count} transaction{(Items.Count == 1 ? "" : "s")}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction history load failed");
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

    private async void Edit(TransactionItem item) =>
        await _nav.GoToAsync($"{AppConstants.Routes.Transaction}?id={item.Id}");

    private async void Delete(TransactionItem item)
    {
        if (!await _dialogs.ConfirmAsync("Delete transaction",
                $"Delete \"{item.Description}\" ({item.AmountText})? This cannot be undone.", "Delete", "Cancel")) return;

        var result = await _account.DeleteAsync(_session.RequireUserId(), item.Id);
        if (!result.Success) ErrorMessage = result.Message;
    }

    [RelayCommand] private Task AddAsync() => _nav.GoToAsync(AppConstants.Routes.Transaction);
    [RelayCommand] private Task BackAsync() => _nav.GoToAsync(AppConstants.Routes.Back);
}
