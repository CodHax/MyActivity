using MyActivity.Models;
using MyActivity.Services;

namespace MyActivity.Repositories;

public interface INotificationScheduleRepository
{
    Task InsertAsync(NotificationSchedule row);   // populates row.Id
    Task<NotificationSchedule?> FindAsync(int userId, string category, string referenceKey);
    Task<List<NotificationSchedule>> GetByCategoryPrefixAsync(int userId, string prefix);
    Task<List<NotificationSchedule>> GetForUserAsync(int userId);
    Task DeleteAsync(IEnumerable<int> ids);
}

public class NotificationScheduleRepository : INotificationScheduleRepository
{
    private readonly IDatabaseService _database;
    public NotificationScheduleRepository(IDatabaseService database) => _database = database;

    public async Task InsertAsync(NotificationSchedule row)
    {
        var db = await _database.GetConnectionAsync();
        await db.InsertAsync(row);
    }

    public async Task<NotificationSchedule?> FindAsync(int userId, string category, string referenceKey)
    {
        var db = await _database.GetConnectionAsync();
        return await db.FindWithQueryAsync<NotificationSchedule>(
            "SELECT * FROM NotificationSchedules WHERE UserId = ? AND Category = ? AND ReferenceKey = ? LIMIT 1",
            userId, category, referenceKey);
    }

    public async Task<List<NotificationSchedule>> GetByCategoryPrefixAsync(int userId, string prefix)
    {
        var db = await _database.GetConnectionAsync();
        return await db.QueryAsync<NotificationSchedule>(
            "SELECT * FROM NotificationSchedules WHERE UserId = ? AND Category LIKE ?", userId, prefix + "%");
    }

    public async Task<List<NotificationSchedule>> GetForUserAsync(int userId)
    {
        var db = await _database.GetConnectionAsync();
        return await db.QueryAsync<NotificationSchedule>(
            "SELECT * FROM NotificationSchedules WHERE UserId = ?", userId);
    }

    public async Task DeleteAsync(IEnumerable<int> ids)
    {
        var db = await _database.GetConnectionAsync();
        await db.RunInTransactionAsync(conn =>
        {
            foreach (var id in ids) conn.Execute("DELETE FROM NotificationSchedules WHERE Id = ?", id);
        });
    }
}
