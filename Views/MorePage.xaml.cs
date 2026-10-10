using MyActivity.ViewModels;

namespace MyActivity.Views;

public partial class MorePage : ContentPage
{
    private readonly MoreViewModel _viewModel;

    public MorePage(MoreViewModel viewModel)
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
