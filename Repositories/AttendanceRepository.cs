using MyActivity.Models;
using MyActivity.Services;

namespace MyActivity.Repositories;

public interface IAttendanceRepository
{
    /// <summary>
    /// Inserts a record. If one already exists for (user, date, type): overwrites it when
    /// <paramref name="overwrite"/> is true, otherwise leaves it untouched. Returns true if a row was written.
    /// </summary>
    Task<bool> UpsertAsync(Attendance record, bool overwrite);
    Task<Attendance?> GetAsync(int userId, string dateKey, string type);
    Task<List<Attendance>> GetRangeAsync(int userId, string fromKey, string toKey);
}

public class AttendanceRepository : IAttendanceRepository
{
    private readonly IDatabaseService _database;
    public AttendanceRepository(IDatabaseService database) => _database = database;

    public async Task<bool> UpsertAsync(Attendance r, bool overwrite)
    {
        var db = await _database.GetConnectionAsync();

        var onConflict = overwrite
            ? @"DO UPDATE SET CheckInTime = excluded.CheckInTime, CheckOutTime = excluded.CheckOutTime,
                              ResponseTime = excluded.ResponseTime, Status = excluded.Status,
                              Source = excluded.Source, UpdatedAt = excluded.UpdatedAt"
            : "DO NOTHING";

        var sql = $@"INSERT INTO Attendance
            (UserId, AttendanceDate, AttendanceType, CheckInTime, CheckOutTime, ResponseTime, Status, Source, CreatedAt, UpdatedAt)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(UserId, AttendanceDate, AttendanceType) {onConflict}";

        var rows = await db.ExecuteAsync(sql,
            r.UserId, r.AttendanceDate, r.AttendanceType, r.CheckInTime, r.CheckOutTime,
            r.ResponseTime, r.Status, r.Source, r.CreatedAt, r.UpdatedAt);
        return rows > 0;
    }

    public async Task<Attendance?> GetAsync(int userId, string dateKey, string type)
    {
        var db = await _database.GetConnectionAsync();
        return await db.FindWithQueryAsync<Attendance>(
            "SELECT * FROM Attendance WHERE UserId = ? AND AttendanceDate = ? AND AttendanceType = ? LIMIT 1",
            userId, dateKey, type);
    }

    public async Task<List<Attendance>> GetRangeAsync(int userId, string fromKey, string toKey)
    {
        var db = await _database.GetConnectionAsync();
        return await db.QueryAsync<Attendance>(
            "SELECT * FROM Attendance WHERE UserId = ? AND AttendanceDate >= ? AND AttendanceDate <= ? ORDER BY AttendanceDate DESC",
            userId, fromKey, toKey);
    }
}
