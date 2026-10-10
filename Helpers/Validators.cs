using System.Net.Mail;
using System.Text.RegularExpressions;

namespace MyActivity.Helpers;

/// <summary>Each method returns an error message, or null when the value is valid.</summary>
public static partial class Validators
{
    [GeneratedRegex(@"^[A-Za-z0-9._-]{2,30}$")]
    private static partial Regex EmployeeIdRegex();

    public static string? ValidateEmployeeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Please enter your Employee ID.";
        return EmployeeIdRegex().IsMatch(value.Trim())
            ? null
            : "Employee ID can use letters, numbers, dot, dash or underscore (2-30 characters).";
    }

    public static string? ValidateFullName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Please enter your full name.";
        var v = value.Trim();
        return v.Length is < 2 or > 100 ? "Name must be 2-100 characters." : null;
    }

    public static string? ValidateEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Please enter your email.";
        var v = value.Trim();
        if (v.Length > 254 || !MailAddress.TryCreate(v, out var addr) ||
            !string.Equals(addr.Address, v, StringComparison.OrdinalIgnoreCase) ||
            !addr.Host.Contains('.'))
            return "Invalid email address.";
        return null;
    }

    public static string? ValidatePassword(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "Please enter your password.";
        if (value.Length < 8) return "Password must be at least 8 characters.";
        if (value.Length > 128) return "Password is too long (max 128 characters).";
        if (!value.Any(char.IsLetter) || !value.Any(char.IsDigit))
            return "Password must contain at least one letter and one number.";
        return null;
    }

    public static string? ValidateConfirmPassword(string? password, string? confirm)
    {
        if (string.IsNullOrEmpty(confirm)) return "Please confirm your password.";
        return password == confirm ? null : "Passwords do not match.";
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    public static string NormalizeName(string name) => Regex.Replace(name.Trim(), @"\s+", " ");
}
