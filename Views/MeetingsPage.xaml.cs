using MyActivity.ViewModels;

namespace MyActivity.Views;

public partial class MeetingsPage : ContentPage
{
    private readonly MeetingsViewModel _viewModel;

    public MeetingsPage(MeetingsViewModel viewModel)
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
