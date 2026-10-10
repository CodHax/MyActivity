using CommunityToolkit.Mvvm.ComponentModel;

namespace MyActivity.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsNotBusy => !IsBusy;
}
