using MyActivity.Models;
using MyActivity.Services;

namespace MyActivity.Repositories;

public interface ISettingsRepository
{
    Task<AppSetting?> GetByUserIdAsync(int userId);
    Task InsertAsync(AppSetting setting);
    Task UpdateAsync(AppSetting setting);
}

public class SettingsRepository : ISettingsRepository
{
    private readonly IDatabaseService _database;
    public SettingsRepository(IDatabaseService database) => _database = database;

    public async Task<AppSetting?> GetByUserIdAsync(int userId)
    {
        var db = await _database.GetConnectionAsync();
        return await db.FindWithQueryAsync<AppSetting>("SELECT * FROM AppSettings WHERE UserId = ? LIMIT 1", userId);
    }

    public async Task InsertAsync(AppSetting setting)
    {
        var db = await _database.GetConnectionAsync();
        await db.InsertAsync(setting);
    }

    public async Task UpdateAsync(AppSetting setting)
    {
        var db = await _database.GetConnectionAsync();
        setting.UpdatedAt = DateTime.UtcNow;
        await db.UpdateAsync(setting);
    }
}
