namespace MyActivity.Services;

public interface INavigationService
{
    string CurrentLocation { get; }
    Task GoToAsync(string route);
}

public class NavigationService : INavigationService
{
    public string CurrentLocation => Shell.Current?.CurrentState?.Location?.ToString() ?? string.Empty;
    public Task GoToAsync(string route) => Shell.Current.GoToAsync(route);
}
