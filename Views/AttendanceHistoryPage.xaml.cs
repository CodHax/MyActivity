using MyActivity.ViewModels;

namespace MyActivity.Views;

public partial class AttendanceHistoryPage : ContentPage
{
    private readonly AttendanceHistoryViewModel _viewModel;

    public AttendanceHistoryPage(AttendanceHistoryViewModel viewModel)
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
