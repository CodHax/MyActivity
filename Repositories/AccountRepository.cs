using System.Text;
using MyActivity.Models;
using MyActivity.Services;

namespace MyActivity.Repositories;

public record TransactionFilter(string? FromKey = null, string? ToKey = null, string? Type = null, string? Category = null);

public interface IAccountRepository
{
    Task InsertAsync(AccountTransaction t);
    Task UpdateAsync(AccountTransaction t);
    Task<AccountTransaction?> GetAsync(int userId, int id);
    Task<bool> DeleteAsync(int userId, int id);
    Task<List<AccountTransaction>> QueryAsync(int userId, TransactionFilter filter, int limit = 500);
    Task<(long CreditMinor, long DebitMinor)> GetTotalsAsync(int userId, TransactionFilter filter);
}

public class AccountRepository : IAccountRepository
{
    private readonly IDatabaseService _database;
    public AccountRepository(IDatabaseService database) => _database = database;

    public async Task InsertAsync(AccountTransaction t)
    {
        var db = await _database.GetConnectionAsync();
        await db.InsertAsync(t);
    }

    public async Task UpdateAsync(AccountTransaction t)
    {
        var db = await _database.GetConnectionAsync();
        await db.UpdateAsync(t);
    }

    public async Task<AccountTransaction?> GetAsync(int userId, int id)
    {
        var db = await _database.GetConnectionAsync();
        return await db.FindWithQueryAsync<AccountTransaction>(
            "SELECT * FROM AccountTransactions WHERE Id = ? AND UserId = ? LIMIT 1", id, userId);
    }

    public async Task<bool> DeleteAsync(int userId, int id)
    {
        var db = await _database.GetConnectionAsync();
        return await db.ExecuteAsync("DELETE FROM AccountTransactions WHERE Id = ? AND UserId = ?", id, userId) > 0;
    }

    public async Task<List<AccountTransaction>> QueryAsync(int userId, TransactionFilter f, int limit = 500)
    {
        var db = await _database.GetConnectionAsync();
        var (where, args) = BuildWhere(userId, f);
        args.Add(limit);
        return await db.QueryAsync<AccountTransaction>(
            $"SELECT * FROM AccountTransactions WHERE {where} ORDER BY TransactionDate DESC, TransactionTime DESC, Id DESC LIMIT ?",
            args.ToArray());
    }

    public async Task<(long CreditMinor, long DebitMinor)> GetTotalsAsync(int userId, TransactionFilter f)
    {
        var db = await _database.GetConnectionAsync();
        var (where, args) = BuildWhere(userId, f with { Type = null });   // totals always cover both types

        var credit = await db.ExecuteScalarAsync<long>(
            $"SELECT COALESCE(SUM(AmountMinor), 0) FROM AccountTransactions WHERE {where} AND TransactionType = 'Credit'",
            args.ToArray());
        var debit = await db.ExecuteScalarAsync<long>(
            $"SELECT COALESCE(SUM(AmountMinor), 0) FROM AccountTransactions WHERE {where} AND TransactionType = 'Debit'",
            args.ToArray());
        return (credit, debit);
    }

    /// <summary>Builds a fully parameterised WHERE clause (no user text is ever concatenated into SQL).</summary>
    private static (string Sql, List<object> Args) BuildWhere(int userId, TransactionFilter f)
    {
        var sb = new StringBuilder("UserId = ?");
        var args = new List<object> { userId };
        if (!string.IsNullOrEmpty(f.FromKey)) { sb.Append(" AND TransactionDate >= ?"); args.Add(f.FromKey); }
        if (!string.IsNullOrEmpty(f.ToKey)) { sb.Append(" AND TransactionDate <= ?"); args.Add(f.ToKey); }
        if (!string.IsNullOrEmpty(f.Type)) { sb.Append(" AND TransactionType = ?"); args.Add(f.Type); }
        if (!string.IsNullOrEmpty(f.Category)) { sb.Append(" AND Category = ?"); args.Add(f.Category); }
        return (sb.ToString(), args);
    }
}
