using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Services;

namespace MyActivity.ViewModels;

public partial class BasicCalculatorViewModel : BaseViewModel
{
    private readonly BasicCalculator _calc = new();
    private readonly INavigationService _nav;

    public BasicCalculatorViewModel(INavigationService nav) => _nav = nav;

    [ObservableProperty] private string _display = "0";
    [ObservableProperty] private string _expression = string.Empty;
    [ObservableProperty] private double _displayFontSize = 56;

    /// <summary>Keys: 0-9 . + - * / = % C B N</summary>
    [RelayCommand]
    private void Key(string? key)
    {
        _calc.Press(key ?? string.Empty);
        Display = _calc.Display;
        Expression = _calc.Expression;
        DisplayFontSize = _calc.HasError ? 26 : Display.Length switch { <= 9 => 56, <= 12 => 42, _ => 32 };
    }

    [RelayCommand] private Task BackAsync() => _nav.GoToAsync(AppConstants.Routes.Back);
}

/// <summary>Shared behaviour for EMI / SIP / SWP: Months-Years unit chips, parsing, back navigation.</summary>
public abstract partial class FinanceCalculatorViewModel : BaseViewModel
{
    private readonly INavigationService _nav;

    protected FinanceCalculatorViewModel(INavigationService nav)
    {
        _nav = nav;
        Units = new ObservableCollection<FilterOption>
        {
            new("months", "Months", SelectUnit),
            new("years", "Years", SelectUnit)
        };
        Units[1].IsSelected = true;
    }

    public ObservableCollection<FilterOption> Units { get; }
    public bool IsYears => Units[1].IsSelected;

    private void SelectUnit(FilterOption option)
    {
        foreach (var u in Units) u.IsSelected = ReferenceEquals(u, option);
        OnPropertyChanged(nameof(IsYears));
        OnUnitChanged();
    }

    protected virtual void OnUnitChanged() { }
    protected static decimal? Parse(string? text) => MoneyFormat.Parse(text);

    [RelayCommand] private Task BackAsync() => _nav.GoToAsync(AppConstants.Routes.Back);
}

public partial class EmiCalculatorViewModel : FinanceCalculatorViewModel
{
    public EmiCalculatorViewModel(INavigationService nav) : base(nav) { }

    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private string _rateText = string.Empty;
    [ObservableProperty] private string _tenureText = string.Empty;
    [ObservableProperty] private string? _amountError;
    [ObservableProperty] private string? _rateError;
    [ObservableProperty] private string? _tenureError;

    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private string _emiText = string.Empty;
    [ObservableProperty] private string _principalText = string.Empty;
    [ObservableProperty] private string _interestText = string.Empty;
    [ObservableProperty] private string _totalText = string.Empty;
    [ObservableProperty] private double _principalFraction;
    [ObservableProperty] private string _splitText = string.Empty;

    [RelayCommand]
    private void Calculate()
    {
        var amount = Parse(AmountText);
        var rate = Parse(RateText);
        var tenure = Parse(TenureText);

        AmountError = FinancialCalculators.ValidateAmount(amount, "the loan amount");
        RateError = FinancialCalculators.ValidateRate(rate, allowZero: true);
        TenureError = FinancialCalculators.ValidateTenure(tenure, IsYears);
        if (AmountError is not null || RateError is not null || TenureError is not null) { HasResult = false; return; }

        var months = FinancialCalculators.ToMonths(tenure!.Value, IsYears);
        var r = FinancialCalculators.Emi(amount!.Value, rate!.Value, months);

        EmiText = MoneyFormat.Format(r.Emi);
        PrincipalText = MoneyFormat.Format(r.Principal);
        InterestText = MoneyFormat.Format(r.TotalInterest);
        TotalText = MoneyFormat.Format(r.TotalPayment);
        PrincipalFraction = r.TotalPayment == 0 ? 1 : (double)(r.Principal / r.TotalPayment);
        SplitText = $"Principal {PrincipalFraction:P0}   |   Interest {1 - PrincipalFraction:P0}";
        HasResult = true;
    }

    [RelayCommand]
    private void Reset()
    {
        AmountText = RateText = TenureText = string.Empty;
        AmountError = RateError = TenureError = null;
        HasResult = false;
    }
}

public partial class SipCalculatorViewModel : FinanceCalculatorViewModel
{
    public SipCalculatorViewModel(INavigationService nav) : base(nav)
    {
        Modes = new ObservableCollection<FilterOption>
        {
            new("monthly", "Monthly SIP", SelectMode),
            new("lump", "One-time", SelectMode)
        };
        Modes[0].IsSelected = true;
    }

    public ObservableCollection<FilterOption> Modes { get; }
    public bool IsMonthly => Modes[0].IsSelected;
    public string AmountLabel => IsMonthly ? "Monthly investment" : "Investment amount";

    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private string _rateText = string.Empty;
    [ObservableProperty] private string _durationText = string.Empty;
    [ObservableProperty] private string? _amountError;
    [ObservableProperty] private string? _rateError;
    [ObservableProperty] private string? _durationError;

    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private string _investedText = string.Empty;
    [ObservableProperty] private string _returnsText = string.Empty;
    [ObservableProperty] private string _maturityText = string.Empty;
    [ObservableProperty] private double _investedFraction;
    [ObservableProperty] private string _splitText = string.Empty;

    private void SelectMode(FilterOption o)
    {
        foreach (var m in Modes) m.IsSelected = ReferenceEquals(m, o);
        OnPropertyChanged(nameof(IsMonthly));
        OnPropertyChanged(nameof(AmountLabel));
        HasResult = false;
    }

    [RelayCommand]
    private void Calculate()
    {
        var amount = Parse(AmountText);
        var rate = Parse(RateText);
        var duration = Parse(DurationText);

        AmountError = FinancialCalculators.ValidateAmount(amount, IsMonthly ? "the monthly investment" : "the investment amount");
        RateError = FinancialCalculators.ValidateRate(rate, allowZero: true);
        DurationError = FinancialCalculators.ValidateTenure(duration, IsYears);
        if (AmountError is not null || RateError is not null || DurationError is not null) { HasResult = false; return; }

        var months = FinancialCalculators.ToMonths(duration!.Value, IsYears);
        var r = IsMonthly
            ? FinancialCalculators.MonthlySip(amount!.Value, rate!.Value, months)
            : FinancialCalculators.LumpSum(amount!.Value, rate!.Value, months);

        InvestedText = MoneyFormat.Format(r.TotalInvested);
        ReturnsText = MoneyFormat.Format(r.EstimatedReturns);
        MaturityText = MoneyFormat.Format(r.MaturityValue);
        InvestedFraction = r.MaturityValue == 0 ? 1 : Math.Clamp((double)(r.TotalInvested / r.MaturityValue), 0, 1);
        SplitText = $"Invested {InvestedFraction:P0}   |   Returns {1 - InvestedFraction:P0}";
        HasResult = true;
    }

    [RelayCommand]
    private void Reset()
    {
        AmountText = RateText = DurationText = string.Empty;
        AmountError = RateError = DurationError = null;
        HasResult = false;
    }
}

public partial class SwpCalculatorViewModel : FinanceCalculatorViewModel
{
    public SwpCalculatorViewModel(INavigationService nav) : base(nav) { }

    [ObservableProperty] private string _initialText = string.Empty;
    [ObservableProperty] private string _rateText = string.Empty;
    [ObservableProperty] private string _withdrawalText = string.Empty;
    [ObservableProperty] private string _durationText = string.Empty;
    [ObservableProperty] private string? _initialError;
    [ObservableProperty] private string? _rateError;
    [ObservableProperty] private string? _withdrawalError;
    [ObservableProperty] private string? _durationError;

    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private string _withdrawnText = string.Empty;
    [ObservableProperty] private string _remainingText = string.Empty;
    [ObservableProperty] private string _growthText = string.Empty;
    [ObservableProperty] private string _noteText = string.Empty;
    [ObservableProperty] private bool _hasNote;

    [RelayCommand]
    private void Calculate()
    {
        var initial = Parse(InitialText);
        var rate = Parse(RateText);
        var withdrawal = Parse(WithdrawalText);
        var duration = Parse(DurationText);

        InitialError = FinancialCalculators.ValidateAmount(initial, "the initial investment");
        RateError = FinancialCalculators.ValidateRate(rate, allowZero: true);
        WithdrawalError = FinancialCalculators.ValidateAmount(withdrawal, "the monthly withdrawal");
        DurationError = FinancialCalculators.ValidateTenure(duration, IsYears);
        if (InitialError is not null || RateError is not null || WithdrawalError is not null || DurationError is not null)
        {
            HasResult = false;
            return;
        }

        var months = FinancialCalculators.ToMonths(duration!.Value, IsYears);
        var r = FinancialCalculators.Swp(initial!.Value, rate!.Value, withdrawal!.Value, months);

        WithdrawnText = MoneyFormat.Format(r.TotalWithdrawn);
        RemainingText = MoneyFormat.Format(r.RemainingAmount);
        GrowthText = MoneyFormat.Format(r.EstimatedReturns);

        HasNote = r.CorpusExhausted;
        NoteText = r.CorpusExhausted
            ? $"Your investment runs out after {r.MonthsWithdrawn / 12} years {r.MonthsWithdrawn % 12} months at this withdrawal."
            : string.Empty;
        HasResult = true;
    }

    [RelayCommand]
    private void Reset()
    {
        InitialText = RateText = WithdrawalText = DurationText = string.Empty;
        InitialError = RateError = WithdrawalError = DurationError = null;
        HasResult = false;
    }
}
