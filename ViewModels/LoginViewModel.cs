using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Services;

namespace MyActivity.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthenticationService _auth;
    private readonly INavigationService _nav;

    public LoginViewModel(IAuthenticationService auth, INavigationService nav)
    {
        _auth = auth;
        _nav = nav;
    }

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _rememberMe = true;
    [ObservableProperty] private string? _emailError;
    [ObservableProperty] private string? _passwordError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordToggleText))]
    private bool _isPasswordHidden = true;

    public string PasswordToggleText => IsPasswordHidden ? "Show" : "Hide";

    [RelayCommand]
    private void TogglePassword() => IsPasswordHidden = !IsPasswordHidden;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;

        ErrorMessage = null;
        EmailError = Validators.ValidateEmail(Email);
        PasswordError = string.IsNullOrEmpty(Password) ? "Please enter your password." : null;
        if (EmailError is not null || PasswordError is not null) return;

        IsBusy = true;
        try
        {
            var result = await _auth.LoginAsync(Email, Password, RememberMe);
            if (!result.Success)
            {
                ErrorMessage = result.Message;
                return;
            }

            Password = string.Empty;
            await _nav.GoToAsync(AppConstants.Routes.Dashboard);
        }
        catch
        {
            ErrorMessage = "Unable to log in. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task GoToSignupAsync() => _nav.GoToAsync(AppConstants.Routes.Signup);

    [RelayCommand]
    private Task GoToResetPasswordAsync() => _nav.GoToAsync(AppConstants.Routes.ResetPassword);
}
