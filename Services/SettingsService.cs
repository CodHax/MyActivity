using MyActivity.Models;
using MyActivity.Repositories;

namespace MyActivity.Services;

public interface ISettingsService
{
    Task<AppSetting> GetAsync(int userId);
    Task SaveAsync(AppSetting setting);
}

public class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _settings;
    public SettingsService(ISettingsRepository settings) => _settings = settings;

    public async Task<AppSetting> GetAsync(int userId)
    {
        var s = await _settings.GetByUserIdAsync(userId);
        if (s is not null) return s;
        s = AppSetting.CreateDefault(userId);
        await _settings.InsertAsync(s);
        return s;
    }

    public Task SaveAsync(AppSetting setting) => _settings.UpdateAsync(setting);
}
