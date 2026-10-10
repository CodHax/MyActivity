using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Models;
using MyActivity.Repositories;

namespace MyActivity.Services;

/// <summary>Holds the signed-in user. Only the user id (never the password) is persisted, in SecureStorage.</summary>
public interface ISessionService
{
    User? CurrentUser { get; }
    bool IsAuthenticated { get; }
    /// <summary>The signed-in user's id; throws if nobody is signed in. Use this for every user-scoped query.</summary>
    int RequireUserId();
    Task StartAsync(User user, bool remember);
    Task<bool> TryRestoreAsync();
    Task EndAsync();
}

public class SessionService : ISessionService
{
    private readonly IUserRepository _users;
    private readonly ILogger<SessionService> _logger;

    public SessionService(IUserRepository users, ILogger<SessionService> logger)
    {
        _users = users;
        _logger = logger;
    }

    public User? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;

    public int RequireUserId() =>
        CurrentUser?.Id ?? throw new InvalidOperationException("No signed-in user.");

    public async Task StartAsync(User user, bool remember)
    {
        CurrentUser = user;
        if (remember)
        {
            try { await SecureStorage.Default.SetAsync(AppConstants.SessionUserIdKey, user.Id.ToString()); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not persist session"); }
        }
        else
        {
            RemovePersistedSession();
        }
    }

    public async Task<bool> TryRestoreAsync()
    {
        try
        {
            var stored = await SecureStorage.Default.GetAsync(AppConstants.SessionUserIdKey);
            if (!int.TryParse(stored, out var userId)) return false;

            var user = await _users.GetByIdAsync(userId);
            if (user is null || !user.IsActive)
            {
                RemovePersistedSession();
                return false;
            }

            CurrentUser = user;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Session restore failed");
            RemovePersistedSession();
            return false;
        }
    }

    public Task EndAsync()
    {
        CurrentUser = null;
        RemovePersistedSession();
        return Task.CompletedTask;
    }

    private void RemovePersistedSession()
    {
        try { SecureStorage.Default.Remove(AppConstants.SessionUserIdKey); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not clear persisted session"); }
    }
}
