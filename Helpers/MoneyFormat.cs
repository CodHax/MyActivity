using System.Globalization;

namespace MyActivity.Helpers;

/// <summary>Indian-rupee formatting (lakh/crore grouping) and money conversions.</summary>
public static class MoneyFormat
{
    private static readonly CultureInfo Inr = Create();

    private static CultureInfo Create()
    {
        try { return new CultureInfo("en-IN"); }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
    }

    public static string Format(decimal amount)
    {
        var abs = Math.Abs(amount).ToString("N2", Inr);
        return (amount < 0 ? "-" : string.Empty) + "₹" + abs;
    }

    public static string FormatMinor(long minor) => Format(minor / 100m);

    public static long ToMinor(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    public static decimal FromMinor(long minor) => minor / 100m;

    /// <summary>Accepts "1,500.50", "1500.5", "1500,5". Returns null for empty/invalid input.</summary>
    public static decimal? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim().Replace("₹", "").Replace(" ", "");
        if (decimal.TryParse(t, NumberStyles.Number, CultureInfo.CurrentCulture, out var v)) return v;
        if (decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out v)) return v;
        return null;
    }

    public static bool HasMoreThanTwoDecimals(decimal v) => decimal.Round(v, 2) != v;
}
