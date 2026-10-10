using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Models;
using MyActivity.Repositories;

namespace MyActivity.Services;

public record SignupRequest(string EmployeeId, string FullName, string Email, string Password, string ConfirmPassword);

public interface IAuthenticationService
{
    Task<ServiceResult<User>> RegisterAsync(SignupRequest request);
    Task<ServiceResult<User>> LoginAsync(string email, string password, bool rememberMe);
    /// <summary>Offline reset: proves ownership by matching Employee ID + email on this device.</summary>
    Task<ServiceResult> ResetPasswordAsync(string employeeId, string email, string newPassword, string confirmPassword);
    Task<ServiceResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword, string confirmPassword);
    Task LogoutAsync();
}

public class AuthenticationService : IAuthenticationService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(60);

    private readonly IUserRepository _users;
    private readonly ISessionService _session;
    private readonly IReminderCoordinator _reminders;
    private readonly IThemeService _theme;
    private readonly ILogger<AuthenticationService> _logger;

    // In-memory brute-force guard (resets when the app restarts).
    private readonly ConcurrentDictionary<string, (int Fails, DateTime LockedUntil)> _attempts = new();

    public AuthenticationService(IUserRepository users, ISessionService session, IReminderCoordinator reminders,
        IThemeService theme, ILogger<AuthenticationService> logger)
    {
        _users = users;
        _session = session;
        _reminders = reminders;
        _theme = theme;
        _logger = logger;
    }

    public async Task<ServiceResult<User>> RegisterAsync(SignupRequest r)
    {
        // The service never trusts the UI: validate again.
        var error = Validators.ValidateEmployeeId(r.EmployeeId)
                    ?? Validators.ValidateFullName(r.FullName)
                    ?? Validators.ValidateEmail(r.Email)
                    ?? Validators.ValidatePassword(r.Password)
                    ?? Validators.ValidateConfirmPassword(r.Password, r.ConfirmPassword);
        if (error is not null) return ServiceResult<User>.Fail(error);

        try
        {
            var employeeId = r.EmployeeId.Trim();
            var email = Validators.NormalizeEmail(r.Email);

            if (await _users.EmployeeIdExistsAsync(employeeId))
                return ServiceResult<User>.Fail("Employee ID already exists.");
            if (await _users.EmailExistsAsync(email))
                return ServiceResult<User>.Fail("Email is already registered.");

            var now = DateTime.UtcNow;
            var user = new User
            {
                EmployeeId = employeeId,
                FullName = Validators.NormalizeName(r.FullName),
                Email = email,
                PasswordHash = await Task.Run(() => PasswordHasher.Hash(r.Password)),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true
            };

            await _users.CreateWithDefaultSettingsAsync(user);
            return ServiceResult<User>.Ok(user, AppConstants.Messages.AccountCreated);
        }
        catch (DuplicateUserException)
        {
            return ServiceResult<User>.Fail("Employee ID or email already exists.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed");
            return ServiceResult<User>.Fail(AppConstants.Messages.SaveFailed);
        }
    }

    public async Task<ServiceResult<User>> LoginAsync(string email, string password, bool rememberMe)
    {
        var error = Validators.ValidateEmail(email) ?? (string.IsNullOrEmpty(password) ? "Please enter your password." : null);
        if (error is not null) return ServiceResult<User>.Fail(error);

        var key = Validators.NormalizeEmail(email);
        if (_attempts.TryGetValue(key, out var state) && state.LockedUntil > DateTime.UtcNow)
        {
            var seconds = (int)Math.Ceiling((state.LockedUntil - DateTime.UtcNow).TotalSeconds);
            return ServiceResult<User>.Fail($"Too many failed attempts. Try again in {seconds} seconds.");
        }

        try
        {
            var user = await _users.GetByEmailAsync(key);
            if (user is null) return ServiceResult<User>.Fail("No account found with this email.");
            if (!user.IsActive) return ServiceResult<User>.Fail("This account is deactivated.");

            var valid = await Task.Run(() => PasswordHasher.Verify(password, user.PasswordHash));
            if (!valid)
            {
                RegisterFailure(key);
                return ServiceResult<User>.Fail("Incorrect password.");
            }
            _attempts.TryRemove(key, out _);

            if (PasswordHasher.NeedsRehash(user.PasswordHash))
            {
                user.PasswordHash = await Task.Run(() => PasswordHasher.Hash(password));
                await _users.UpdateAsync(user);
            }

            await _session.StartAsync(user, rememberMe);

            // Reminder/theme problems must never block a successful login.
            try
            {
                await _theme.ApplyForUserAsync(user.Id);
                await _reminders.RescheduleAllAsync(user.Id);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Post-login setup failed"); }

            return ServiceResult<User>.Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return ServiceResult<User>.Fail("Unable to log in. Please try again.");
        }
    }

    public async Task<ServiceResult> ResetPasswordAsync(string employeeId, string email, string newPassword, string confirmPassword)
    {
        var error = Validators.ValidateEmployeeId(employeeId)
                    ?? Validators.ValidateEmail(email)
                    ?? Validators.ValidatePassword(newPassword)
                    ?? Validators.ValidateConfirmPassword(newPassword, confirmPassword);
        if (error is not null) return ServiceResult.Fail(error);

        try
        {
            var user = await _users.GetByEmployeeIdAsync(employeeId.Trim());
            if (user is null || !string.Equals(user.Email, Validators.NormalizeEmail(email), StringComparison.OrdinalIgnoreCase))
                return ServiceResult.Fail("Employee ID and email do not match any account.");

            user.PasswordHash = await Task.Run(() => PasswordHasher.Hash(newPassword));
            await _users.UpdateAsync(user);
            _attempts.TryRemove(user.Email, out _);
            return ServiceResult.Ok(AppConstants.Messages.PasswordReset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Password reset failed");
            return ServiceResult.Fail(AppConstants.Messages.SaveFailed);
        }
    }

    public async Task<ServiceResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword, string confirmPassword)
    {
        if (string.IsNullOrEmpty(currentPassword)) return ServiceResult.Fail("Please enter your current password.");
        var error = Validators.ValidatePassword(newPassword) ?? Validators.ValidateConfirmPassword(newPassword, confirmPassword);
        if (error is not null) return ServiceResult.Fail(error);
        if (currentPassword == newPassword) return ServiceResult.Fail("New password must be different from the current one.");

        try
        {
            var user = await _users.GetByIdAsync(userId);
            if (user is null) return ServiceResult.Fail(AppConstants.Messages.SaveFailed);

            if (!await Task.Run(() => PasswordHasher.Verify(currentPassword, user.PasswordHash)))
                return ServiceResult.Fail("Current password is incorrect.");

            user.PasswordHash = await Task.Run(() => PasswordHasher.Hash(newPassword));
            await _users.UpdateAsync(user);
            return ServiceResult.Ok("Password changed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Change password failed");
            return ServiceResult.Fail(AppConstants.Messages.SaveFailed);
        }
    }

    public async Task LogoutAsync()
    {
        // Remove this user's reminders so they never appear for the next person on the device.
        if (_session.CurrentUser is { } user)
        {
            try { await _reminders.CancelAllAsync(user.Id); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not cancel reminders on logout"); }
        }
        await _session.EndAsync();
        _theme.Reset();
    }

    private void RegisterFailure(string key)
    {
        _attempts.AddOrUpdate(key,
            _ => (1, DateTime.MinValue),
            (_, s) => s.Fails + 1 >= MaxFailedAttempts
                ? (0, DateTime.UtcNow.Add(LockDuration))
                : (s.Fails + 1, DateTime.MinValue));
    }
}
