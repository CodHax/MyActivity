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

public partial class AccountViewModel : BaseViewModel, IRecipient<TransactionsChangedMessage>
{
    private readonly IAccountService _account;
    private readonly ISessionService _session;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly ILogger<AccountViewModel> _logger;

    public AccountViewModel(IAccountService account, ISessionService session, INavigationService nav,
        IDialogService dialogs, ILogger<AccountViewModel> logger)
    {
        _account = account;
        _session = session;
        _nav = nav;
        _dialogs = dialogs;
        _logger = logger;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ObservableCollection<TransactionItem> Recent { get; } = new();

    [ObservableProperty] private string _creditText = MoneyFormat.Format(0);
    [ObservableProperty] private string _debitText = MoneyFormat.Format(0);
    [ObservableProperty] private string _balanceText = MoneyFormat.Format(0);
    [ObservableProperty] private Color _balanceColor = Palette.Success;
    [ObservableProperty] private bool _isEmpty = true;

    public void Receive(TransactionsChangedMessage message) =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());

    public async Task LoadAsync()
    {
        if (!_session.IsAuthenticated) return;
        var userId = _session.RequireUserId();
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var summary = await _account.GetSummaryAsync(userId);
            CreditText = MoneyFormat.Format(summary.Credit);
            DebitText = MoneyFormat.Format(summary.Debit);
            BalanceText = MoneyFormat.Format(summary.Balance);
            BalanceColor = summary.Balance < 0 ? Palette.Danger : Palette.Success;

            var rows = await _account.QueryAsync(userId, new TransactionFilter(), 5);
            Recent.Clear();
            foreach (var t in rows) Recent.Add(new TransactionItem(t, Edit, Delete));
            IsEmpty = Recent.Count == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Account load failed");
            ErrorMessage = AppConstants.Messages.LoadFailed;
        }
        finally
        {
            IsBusy = false;
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
    [RelayCommand] private Task ViewAllAsync() => _nav.GoToAsync(AppConstants.Routes.Transactions);
}
