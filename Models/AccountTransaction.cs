using MyActivity.Constants;
using MyActivity.Helpers;
using SQLite;

namespace MyActivity.Models;

[Table("AccountTransactions")]
public class AccountTransaction
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Credit or Debit.</summary>
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>Amount in paise (integer), so balances never suffer floating-point drift.</summary>
    public long AmountMinor { get; set; }

    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>"yyyy-MM-dd" (local date).</summary>
    public string TransactionDate { get; set; } = string.Empty;

    /// <summary>"HH:mm" (local time).</summary>
    public string TransactionTime { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [Ignore] public decimal Amount => MoneyFormat.FromMinor(AmountMinor);
    [Ignore] public bool IsCredit => TransactionType == AccountConstants.Credit;
}
