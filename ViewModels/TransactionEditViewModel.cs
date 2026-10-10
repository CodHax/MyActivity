using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Services;

namespace MyActivity.ViewModels;

public partial class TransactionEditViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IAccountService _account;
    private readonly ISessionService _session;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly ILogger<TransactionEditViewModel> _logger;
    private int? _editId;
    private bool _loaded;

    public TransactionEditViewModel(IAccountService account, ISessionService session, INavigationService nav,
        IDialogService dialogs, ILogger<TransactionEditViewModel> logger)
    {
        _account = account;
        _session = session;
        _nav = nav;
        _dialogs = dialogs;
        _logger = logger;

        Types = new ObservableCollection<FilterOption>
        {
            new(AccountConstants.Credit, "Credit", SelectType),
            new(AccountConstants.Debit, "Debit", SelectType)
        };
        Types[1].IsSelected = true;   // most entries are expenses
        Date = DateTime.Today;
        Time = DateTime.Now.TimeOfDay;
    }

    public ObservableCollection<FilterOption> Types { get; }
    public IReadOnlyList<string> Categories { get; } = AccountConstants.Categories;
    public DateTime MaxDate => DateTime.Today.AddYears(1);

    [ObservableProperty] private string _screenTitle = "Add transaction";
    [ObservableProperty] private bool _isEditing;

    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private string? _selectedCategory;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private DateTime _date;
    [ObservableProperty] private TimeSpan _time;
    [ObservableProperty] private string _notes = string.Empty;

    [ObservableProperty] private string? _amountError;
    [ObservableProperty] private string? _categoryError;
    [ObservableProperty] private string? _descriptionError;

    private string SelectedType => Types.First(t => t.IsSelected).Key;

    private void SelectType(FilterOption option)
    {
        foreach (var t in Types) t.IsSelected = ReferenceEquals(t, option);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var raw) && int.TryParse(raw?.ToString(), out var id))
        {
            _editId = id;
            IsEditing = true;
            ScreenTitle = "Edit transaction";
        }
    }

    public async Task LoadAsync()
    {
        if (_loaded || _editId is null || !_session.IsAuthenticated) return;
        _loaded = true;
        try
        {
            var t = await _account.GetAsync(_session.RequireUserId(), _editId.Value);
            if (t is null)
            {
                await _dialogs.AlertAsync("Not found", "This transaction no longer exists.");
                await _nav.GoToAsync(AppConstants.Routes.Back);
                return;
            }

            SelectType(Types.First(x => x.Key == t.TransactionType));
            AmountText = t.Amount.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
            SelectedCategory = t.Category;
            Description = t.Description;
            Date = DateTime.ParseExact(t.TransactionDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            Time = DateTime.ParseExact(t.TransactionTime, "HH:mm", System.Globalization.CultureInfo.InvariantCulture).TimeOfDay;
            Notes = t.Notes ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loading transaction failed");
            ErrorMessage = AppConstants.Messages.LoadFailed;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;

        var amount = MoneyFormat.Parse(AmountText);
        AmountError = amount is null ? "Please enter a valid amount."
            : amount <= 0 ? "Amount must be greater than zero."
            : MoneyFormat.HasMoreThanTwoDecimals(amount.Value) ? "Use at most 2 decimal places." : null;
        CategoryError = string.IsNullOrEmpty(SelectedCategory) ? "Please select a category." : null;
        DescriptionError = string.IsNullOrWhiteSpace(Description) ? "Please enter a description." : null;
        if (AmountError is not null || CategoryError is not null || DescriptionError is not null) return;

        IsBusy = true;
        try
        {
            var result = await _account.SaveAsync(_session.RequireUserId(), _editId,
                new TransactionInput(SelectedType, amount, SelectedCategory, Description, Date, Time, Notes));

            if (!result.Success) { ErrorMessage = result.Message; return; }

            await _dialogs.AlertAsync("Saved", result.Message);
            await _nav.GoToAsync(AppConstants.Routes.Back);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving transaction failed");
            ErrorMessage = AppConstants.Messages.SaveFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_editId is null || IsBusy) return;
        if (!await _dialogs.ConfirmAsync("Delete transaction", "Delete this transaction? This cannot be undone.",
                "Delete", "Cancel")) return;

        var result = await _account.DeleteAsync(_session.RequireUserId(), _editId.Value);
        if (!result.Success) { ErrorMessage = result.Message; return; }
        await _nav.GoToAsync(AppConstants.Routes.Back);
    }

    [RelayCommand] private Task BackAsync() => _nav.GoToAsync(AppConstants.Routes.Back);
}
