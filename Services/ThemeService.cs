using MyActivity.Constants;

namespace MyActivity.Services;

public interface IThemeService
{
    Task ApplyForUserAsync(int userId);
    void Apply(string theme);
    void Reset();
}

public class ThemeService : IThemeService
{
    private readonly ISettingsService _settings;
    public ThemeService(ISettingsService settings) => _settings = settings;

    public async Task ApplyForUserAsync(int userId) => Apply((await _settings.GetAsync(userId)).Theme);

    public void Apply(string theme)
    {
        var target = theme switch
        {
            AppConstants.Themes.Light => AppTheme.Light,
            AppConstants.Themes.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Application.Current is { } app) app.UserAppTheme = target;
        });
    }

    public void Reset() => Apply(AppConstants.Themes.System);
}
