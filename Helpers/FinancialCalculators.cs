namespace MyActivity.Helpers;

public record EmiResult(decimal Emi, decimal TotalInterest, decimal TotalPayment, decimal Principal);
public record SipResult(decimal TotalInvested, decimal EstimatedReturns, decimal MaturityValue);
public record SwpResult(decimal TotalWithdrawn, decimal RemainingAmount, decimal EstimatedReturns,
                        int MonthsWithdrawn, bool CorpusExhausted);

/// <summary>Pure, offline financial maths. Inputs must already be validated (see Validate*).</summary>
public static class FinancialCalculators
{
    public const decimal MaxAmount = 100_000_000_000m;   // 10,000 crore
    public const decimal MaxRate = 100m;
    public const int MaxMonths = 600;                    // 50 years

    // ---------- Validation ----------
    public static string? ValidateAmount(decimal? value, string label)
    {
        if (value is null) return $"Please enter {label}.";
        if (value <= 0) return $"{Capitalise(label)} must be greater than zero.";
        if (value > MaxAmount) return $"{Capitalise(label)} is too large.";
        return null;
    }

    public static string? ValidateRate(decimal? value, bool allowZero)
    {
        if (value is null) return "Please enter the interest rate.";
        if (value < 0 || (!allowZero && value == 0)) return "Interest rate must be greater than zero.";
        if (value > MaxRate) return "Interest rate cannot exceed 100%.";
        return null;
    }

    public static string? ValidateTenure(decimal? value, bool isYears)
    {
        if (value is null) return "Please enter the duration.";
        if (value <= 0) return "Duration must be greater than zero.";
        var months = isYears ? value.Value * 12 : value.Value;
        if (months > MaxMonths) return "Duration cannot exceed 50 years.";
        if (months != decimal.Truncate(months)) return isYears
            ? "Duration in years must be a multiple of 1/12 (whole months)."
            : "Months must be a whole number.";
        return null;
    }

    public static int ToMonths(decimal tenure, bool isYears) => (int)(isYears ? tenure * 12 : tenure);

    // ---------- EMI ----------
    /// <summary>EMI = P x r x (1+r)^n / ((1+r)^n - 1), r = annual rate / 12 / 100.</summary>
    public static EmiResult Emi(decimal principal, decimal annualRatePercent, int months)
    {
        double p = (double)principal, n = months;
        double emi;
        if (annualRatePercent == 0)
        {
            emi = p / n;
        }
        else
        {
            var r = (double)annualRatePercent / 12 / 100;
            var pow = Math.Pow(1 + r, n);
            emi = p * r * pow / (pow - 1);
        }

        var emiRounded = Math.Round((decimal)emi, 2, MidpointRounding.AwayFromZero);
        var total = emiRounded * months;
        return new EmiResult(emiRounded, total - principal, total, principal);
    }

    // ---------- SIP ----------
    /// <summary>Monthly SIP, investment at the start of each month: FV = M x [((1+i)^n - 1)/i] x (1+i).</summary>
    public static SipResult MonthlySip(decimal monthlyInvestment, decimal annualReturnPercent, int months)
    {
        double m = (double)monthlyInvestment, n = months;
        var i = (double)annualReturnPercent / 12 / 100;
        var fv = i == 0 ? m * n : m * ((Math.Pow(1 + i, n) - 1) / i) * (1 + i);
        return BuildSip(monthlyInvestment * months, fv);
    }

    /// <summary>One-time (lump sum) investment, compounded annually: FV = P x (1 + R)^(years).</summary>
    public static SipResult LumpSum(decimal amount, decimal annualReturnPercent, int months)
    {
        var fv = (double)amount * Math.Pow(1 + (double)annualReturnPercent / 100, months / 12.0);
        return BuildSip(amount, fv);
    }

    private static SipResult BuildSip(decimal invested, double futureValue)
    {
        var maturity = Math.Round((decimal)futureValue, 2, MidpointRounding.AwayFromZero);
        return new SipResult(invested, maturity - invested, maturity);
    }

    // ---------- SWP ----------
    /// <summary>
    /// Month by month: balance grows by i = rate/12/100, then the withdrawal is taken at month end.
    /// If the balance cannot cover a withdrawal, the remainder is withdrawn and the corpus is exhausted.
    /// </summary>
    public static SwpResult Swp(decimal initial, decimal annualReturnPercent, decimal monthlyWithdrawal, int months)
    {
        var i = (double)annualReturnPercent / 12 / 100;
        double balance = (double)initial, withdrawal = (double)monthlyWithdrawal, withdrawn = 0;
        var done = 0;
        var exhausted = false;

        for (var m = 1; m <= months; m++)
        {
            balance += balance * i;
            var w = Math.Min(withdrawal, balance);
            balance -= w;
            withdrawn += w;
            done = m;
            if (balance <= 0.005) { balance = 0; exhausted = m < months || w < withdrawal; break; }
        }

        var totalWithdrawn = Math.Round((decimal)withdrawn, 2, MidpointRounding.AwayFromZero);
        var remaining = Math.Round((decimal)balance, 2, MidpointRounding.AwayFromZero);
        return new SwpResult(totalWithdrawn, remaining, remaining + totalWithdrawn - initial, done, exhausted);
    }

    private static string Capitalise(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
