using MyActivity.Models;
using MyActivity.Services;

namespace MyActivity.Repositories;

public enum MeetingScope { Today, Upcoming, History }

public interface IMeetingRepository
{
    Task InsertAsync(Meeting m);
    Task UpdateAsync(Meeting m);
    Task<Meeting?> GetAsync(int userId, int id);
    Task<bool> DeleteAsync(int userId, int id);
    Task<List<Meeting>> QueryAsync(int userId, MeetingScope scope, string todayKey);
    /// <summary>Next meeting whose start is at or after the given "yyyy-MM-dd HH:mm" moment.</summary>
    Task<Meeting?> GetNextAsync(int userId, string nowKey);
    Task<List<Meeting>> GetFromDateAsync(int userId, string fromDateKey);
}

public class MeetingRepository : IMeetingRepository
{
    private readonly IDatabaseService _database;
    public MeetingRepository(IDatabaseService database) => _database = database;

    public async Task InsertAsync(Meeting m)
    {
        var db = await _database.GetConnectionAsync();
        await db.InsertAsync(m);
    }

    public async Task UpdateAsync(Meeting m)
    {
        var db = await _database.GetConnectionAsync();
        await db.UpdateAsync(m);
    }

    public async Task<Meeting?> GetAsync(int userId, int id)
    {
        var db = await _database.GetConnectionAsync();
        return await db.FindWithQueryAsync<Meeting>("SELECT * FROM Meetings WHERE Id = ? AND UserId = ? LIMIT 1", id, userId);
    }

    public async Task<bool> DeleteAsync(int userId, int id)
    {
        var db = await _database.GetConnectionAsync();
        return await db.ExecuteAsync("DELETE FROM Meetings WHERE Id = ? AND UserId = ?", id, userId) > 0;
    }

    public async Task<List<Meeting>> QueryAsync(int userId, MeetingScope scope, string todayKey)
    {
        var db = await _database.GetConnectionAsync();
        return scope switch
        {
            MeetingScope.Today => await db.QueryAsync<Meeting>(
                "SELECT * FROM Meetings WHERE UserId = ? AND MeetingDate = ? ORDER BY StartTime, Id", userId, todayKey),
            MeetingScope.Upcoming => await db.QueryAsync<Meeting>(
                "SELECT * FROM Meetings WHERE UserId = ? AND MeetingDate > ? ORDER BY MeetingDate, StartTime, Id", userId, todayKey),
            _ => await db.QueryAsync<Meeting>(
                "SELECT * FROM Meetings WHERE UserId = ? AND MeetingDate < ? ORDER BY MeetingDate DESC, StartTime DESC, Id DESC LIMIT 300",
                userId, todayKey)
        };
    }

    public async Task<Meeting?> GetNextAsync(int userId, string nowKey)
    {
        var db = await _database.GetConnectionAsync();
        return await db.FindWithQueryAsync<Meeting>(
            "SELECT * FROM Meetings WHERE UserId = ? AND (MeetingDate || ' ' || StartTime) >= ? ORDER BY MeetingDate, StartTime LIMIT 1",
            userId, nowKey);
    }

    public async Task<List<Meeting>> GetFromDateAsync(int userId, string fromDateKey)
    {
        var db = await _database.GetConnectionAsync();
        return await db.QueryAsync<Meeting>(
            "SELECT * FROM Meetings WHERE UserId = ? AND MeetingDate >= ? ORDER BY MeetingDate, StartTime", userId, fromDateKey);
    }
}
