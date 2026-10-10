using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Messages;
using MyActivity.Models;
using MyActivity.Repositories;

namespace MyActivity.Services;

public record TransactionInput(string? Type, decimal? Amount, string? Category, string? Description,
    DateTime Date, TimeSpan Time, string? Notes);

public record AccountSummary(decimal Credit, decimal Debit)
{
    /// <summary>Balance = Total Credit - Total Debit.</summary>
    public decimal Balance => Credit - Debit;
}

public interface IAccountService
{
    Task<ServiceResult<AccountTransaction>> SaveAsync(int userId, int? id, TransactionInput input);
    Task<ServiceResult> DeleteAsync(int userId, int id);
    Task<AccountTransaction?> GetAsync(int userId, int id);
    Task<List<AccountTransaction>> QueryAsync(int userId, TransactionFilter filter, int limit = 500);
    Task<AccountSummary> GetSummaryAsync(int userId, TransactionFilter? filter = null);
}

public class AccountService : IAccountService
{
    private readonly IAccountRepository _repo;
    private readonly ILogger<AccountService> _logger;

    public AccountService(IAccountRepository repo, ILogger<AccountService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<ServiceResult<AccountTransaction>> SaveAsync(int userId, int? id, TransactionInput i)
    {
        var error = Validate(i);
        if (error is not null) return ServiceResult<AccountTransaction>.Fail(error);

        try
        {
            var now = DateTime.UtcNow;
            AccountTransaction t;
            if (id is null)
            {
                t = new AccountTransaction { UserId = userId, CreatedAt = now };
            }
            else
            {
                // User-scoped lookup: other users' rows are invisible here.
                var existing = await _repo.GetAsync(userId, id.Value);
                if (existing is null) return ServiceResult<AccountTransaction>.Fail("This transaction no longer exists.");
                t = existing;
            }

            t.TransactionType = i.Type!;
            t.AmountMinor = MoneyFormat.ToMinor(i.Amount!.Value);
            t.Category = i.Category!;
            t.Description = i.Description!.Trim();
            t.TransactionDate = AttendanceConstants.DateKey(i.Date);
            t.TransactionTime = $"{(int)i.Time.TotalHours:00}:{i.Time.Minutes:00}";
            t.Notes = string.IsNullOrWhiteSpace(i.Notes) ? null : i.Notes.Trim();
            t.UpdatedAt = now;

            if (id is null) await _repo.InsertAsync(t); else await _repo.UpdateAsync(t);

            WeakReferenceMessenger.Default.Send(new TransactionsChangedMessage());
            return ServiceResult<AccountTransaction>.Ok(t, "Transaction saved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving transaction failed");
            return ServiceResult<AccountTransaction>.Fail(AppConstants.Messages.SaveFailed);
        }
    }

    public async Task<ServiceResult> DeleteAsync(int userId, int id)
    {
        try
        {
            var ok = await _repo.DeleteAsync(userId, id);
            if (!ok) return ServiceResult.Fail("This transaction no longer exists.");
            WeakReferenceMessenger.Default.Send(new TransactionsChangedMessage());
            return ServiceResult.Ok("Transaction deleted.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deleting transaction failed");
            return ServiceResult.Fail(AppConstants.Messages.SaveFailed);
        }
    }

    public Task<AccountTransaction?> GetAsync(int userId, int id) => _repo.GetAsync(userId, id);

    public Task<List<AccountTransaction>> QueryAsync(int userId, TransactionFilter filter, int limit = 500) =>
        _repo.QueryAsync(userId, filter, limit);

    public async Task<AccountSummary> GetSummaryAsync(int userId, TransactionFilter? filter = null)
    {
        var (credit, debit) = await _repo.GetTotalsAsync(userId, filter ?? new TransactionFilter());
        return new AccountSummary(MoneyFormat.FromMinor(credit), MoneyFormat.FromMinor(debit));
    }

    private static string? Validate(TransactionInput i)
    {
        if (i.Type is not (AccountConstants.Credit or AccountConstants.Debit)) return "Please choose Credit or Debit.";
        if (i.Amount is null) return "Please enter an amount.";
        if (i.Amount <= 0) return "Amount must be greater than zero.";
        if (i.Amount > AccountConstants.MaxAmount) return "Amount is too large.";
        if (MoneyFormat.HasMoreThanTwoDecimals(i.Amount.Value)) return "Amount can have at most 2 decimal places.";
        if (string.IsNullOrWhiteSpace(i.Category) || !AccountConstants.Categories.Contains(i.Category)) return "Please select a category.";
        if (string.IsNullOrWhiteSpace(i.Description)) return "Please enter a description.";
        if (i.Description.Trim().Length > 200) return "Description must be 200 characters or fewer.";
        if (i.Notes is { Length: > 1000 }) return "Notes must be 1000 characters or fewer.";
        return null;
    }
}
