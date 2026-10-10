using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Services;

namespace MyActivity.ViewModels;

public partial class SignupViewModel : BaseViewModel
{
    private readonly IAuthenticationService _auth;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;

    public SignupViewModel(IAuthenticationService auth, INavigationService nav, IDialogService dialogs)
    {
        _auth = auth;
        _nav = nav;
        _dialogs = dialogs;
    }

    [ObservableProperty] private string _employeeId = string.Empty;
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;

    [ObservableProperty] private string? _employeeIdError;
    [ObservableProperty] private string? _fullNameError;
    [ObservableProperty] private string? _emailError;
    [ObservableProperty] private string? _passwordError;
    [ObservableProperty] private string? _confirmPasswordError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordToggleText))]
    private bool _isPasswordHidden = true;

    public string PasswordToggleText => IsPasswordHidden ? "Show" : "Hide";

    [RelayCommand]
    private void TogglePassword() => IsPasswordHidden = !IsPasswordHidden;

    private bool Validate()
    {
        EmployeeIdError = Validators.ValidateEmployeeId(EmployeeId);
        FullNameError = Validators.ValidateFullName(FullName);
        EmailError = Validators.ValidateEmail(Email);
        PasswordError = Validators.ValidatePassword(Password);
        ConfirmPasswordError = Validators.ValidateConfirmPassword(Password, ConfirmPassword);

        return EmployeeIdError is null && FullNameError is null && EmailError is null
               && PasswordError is null && ConfirmPasswordError is null;
    }

    [RelayCommand]
    private async Task SignupAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        if (!Validate()) return;

        IsBusy = true;
        try
        {
            var result = await _auth.RegisterAsync(
                new SignupRequest(EmployeeId, FullName, Email, Password, ConfirmPassword));

            if (!result.Success)
            {
                ErrorMessage = result.Message;
                return;
            }

            await _dialogs.AlertAsync("Welcome to My Activity", result.Message);
            await _nav.GoToAsync(AppConstants.Routes.Back);
        }
        catch
        {
            ErrorMessage = AppConstants.Messages.SaveFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task BackAsync() => _nav.GoToAsync(AppConstants.Routes.Back);
}
