using MyActivity.ViewModels;

namespace MyActivity.Views;

public partial class AttendanceSettingsPage : ContentPage
{
    private readonly AttendanceSettingsViewModel _viewModel;

    public AttendanceSettingsPage(AttendanceSettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
