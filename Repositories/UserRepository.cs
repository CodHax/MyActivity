using MyActivity.Models;
using MyActivity.Services;
using SQLite;

namespace MyActivity.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByEmployeeIdAsync(string employeeId);
    Task<bool> EmailExistsAsync(string email);
    Task<bool> EmployeeIdExistsAsync(string employeeId);
    /// <summary>Inserts the user and its default settings in one transaction.</summary>
    Task CreateWithDefaultSettingsAsync(User user);
    Task UpdateAsync(User user);
}

public class UserRepository : IUserRepository
{
    private readonly IDatabaseService _database;
    public UserRepository(IDatabaseService database) => _database = database;

    public async Task<User?> GetByIdAsync(int id)
    {
        var db = await _database.GetConnectionAsync();
        return await db.FindWithQueryAsync<User>("SELECT * FROM Users WHERE Id = ? LIMIT 1", id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var db = await _database.GetConnectionAsync();
        return await db.FindWithQueryAsync<User>("SELECT * FROM Users WHERE Email = ? LIMIT 1", email);
    }

    public async Task<User?> GetByEmployeeIdAsync(string employeeId)
    {
        var db = await _database.GetConnectionAsync();
        return await db.FindWithQueryAsync<User>("SELECT * FROM Users WHERE EmployeeId = ? LIMIT 1", employeeId);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        var db = await _database.GetConnectionAsync();
        return await db.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Users WHERE Email = ?", email) > 0;
    }

    public async Task<bool> EmployeeIdExistsAsync(string employeeId)
    {
        var db = await _database.GetConnectionAsync();
        return await db.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Users WHERE EmployeeId = ?", employeeId) > 0;
    }

    public async Task CreateWithDefaultSettingsAsync(User user)
    {
        var db = await _database.GetConnectionAsync();
        try
        {
            await db.RunInTransactionAsync(conn =>
            {
                conn.Insert(user);                                   // populates user.Id
                conn.Insert(AppSetting.CreateDefault(user.Id));
            });
        }
        catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
        {
            throw new DuplicateUserException();
        }
    }

    public async Task UpdateAsync(User user)
    {
        var db = await _database.GetConnectionAsync();
        user.UpdatedAt = DateTime.UtcNow;
        await db.UpdateAsync(user);
    }
}
