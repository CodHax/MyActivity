using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyActivity.Constants;
using MyActivity.Helpers;
using MyActivity.Services;

namespace MyActivity.ViewModels;

public partial class ResetPasswordViewModel : BaseViewModel
{
    private readonly IAuthenticationService _auth;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;

    public ResetPasswordViewModel(IAuthenticationService auth, INavigationService nav, IDialogService dialogs)
    {
        _auth = auth;
        _nav = nav;
        _dialogs = dialogs;
    }

    [ObservableProperty] private string _employeeId = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;

    [ObservableProperty] private string? _employeeIdError;
    [ObservableProperty] private string? _emailError;
    [ObservableProperty] private string? _newPasswordError;
    [ObservableProperty] private string? _confirmPasswordError;

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;

        EmployeeIdError = Validators.ValidateEmployeeId(EmployeeId);
        EmailError = Validators.ValidateEmail(Email);
        NewPasswordError = Validators.ValidatePassword(NewPassword);
        ConfirmPasswordError = Validators.ValidateConfirmPassword(NewPassword, ConfirmPassword);
        if (EmployeeIdError is not null || EmailError is not null ||
            NewPasswordError is not null || ConfirmPasswordError is not null) return;

        IsBusy = true;
        try
        {
            var result = await _auth.ResetPasswordAsync(EmployeeId, Email, NewPassword, ConfirmPassword);
            if (!result.Success)
            {
                ErrorMessage = result.Message;
                return;
            }

            await _dialogs.AlertAsync("Password updated", result.Message);
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
