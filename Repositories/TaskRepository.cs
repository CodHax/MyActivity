using MyActivity.Models;
using MyActivity.Services;

namespace MyActivity.Repositories;

public interface ITaskRepository
{
    Task InsertAsync(TaskItem t);
    Task UpdateAsync(TaskItem t);
    Task<TaskItem?> GetAsync(int userId, int id);
    Task<bool> DeleteAsync(int userId, int id);
    /// <param name="status">"Pending", "Completed" or null for all.</param>
    Task<List<TaskItem>> QueryAsync(int userId, string? status);
    Task<TaskItem?> GetNextAsync(int userId, DateTime nowUtc, string todayKey);
    Task<List<TaskItem>> GetPendingWithFutureReminderAsync(int userId, DateTime nowUtc);
}

public class TaskRepository : ITaskRepository
{
    private readonly IDatabaseService _database;
    public TaskRepository(IDatabaseService database) => _database = database;

    public async Task InsertAsync(TaskItem t)
    {
        var db = await _database.GetConnectionAsync();
        await db.InsertAsync(t);
    }

    public async Task UpdateAsync(TaskItem t)
    {
        var db = await _database.GetConnectionAsync();
        await db.UpdateAsync(t);
    }

    public async Task<TaskItem?> GetAsync(int userId, int id)
    {
        var db = await _database.GetConnectionAsync();
        return await db.FindWithQueryAsync<TaskItem>("SELECT * FROM TaskItems WHERE Id = ? AND UserId = ? LIMIT 1", id, userId);
    }

    public async Task<bool> DeleteAsync(int userId, int id)
    {
        var db = await _database.GetConnectionAsync();
        return await db.ExecuteAsync("DELETE FROM TaskItems WHERE Id = ? AND UserId = ?", id, userId) > 0;
    }

    public async Task<List<TaskItem>> QueryAsync(int userId, string? status)
    {
        var db = await _database.GetConnectionAsync();
        // Pending first by priority (High > Medium > Low) then soonest date; completed newest first.
        const string order = @"ORDER BY CASE Status WHEN 'Pending' THEN 0 ELSE 1 END,
                               CASE Priority WHEN 'High' THEN 0 WHEN 'Medium' THEN 1 ELSE 2 END,
                               COALESCE(TaskDate, '9999-12-31'), Id DESC LIMIT 500";
        return status is null
            ? await db.QueryAsync<TaskItem>($"SELECT * FROM TaskItems WHERE UserId = ? {order}", userId)
            : await db.QueryAsync<TaskItem>($"SELECT * FROM TaskItems WHERE UserId = ? AND Status = ? {order}", userId, status);
    }

    public async Task<TaskItem?> GetNextAsync(int userId, DateTime nowUtc, string todayKey)
    {
        var db = await _database.GetConnectionAsync();
        var withReminder = await db.FindWithQueryAsync<TaskItem>(
            "SELECT * FROM TaskItems WHERE UserId = ? AND Status = 'Pending' AND ReminderAt IS NOT NULL AND ReminderAt >= ? ORDER BY ReminderAt LIMIT 1",
            userId, nowUtc);
        if (withReminder is not null) return withReminder;

        return await db.FindWithQueryAsync<TaskItem>(
            "SELECT * FROM TaskItems WHERE UserId = ? AND Status = 'Pending' AND TaskDate IS NOT NULL AND TaskDate >= ? ORDER BY TaskDate LIMIT 1",
            userId, todayKey);
    }

    public async Task<List<TaskItem>> GetPendingWithFutureReminderAsync(int userId, DateTime nowUtc)
    {
        var db = await _database.GetConnectionAsync();
        return await db.QueryAsync<TaskItem>(
            "SELECT * FROM TaskItems WHERE UserId = ? AND Status = 'Pending' AND ReminderAt IS NOT NULL AND ReminderAt > ?",
            userId, nowUtc);
    }
}
