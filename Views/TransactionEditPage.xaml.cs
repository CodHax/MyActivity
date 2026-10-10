using MyActivity.ViewModels;

namespace MyActivity.Views;

public partial class TransactionEditPage : ContentPage
{
    private readonly TransactionEditViewModel _viewModel;

    public TransactionEditPage(TransactionEditViewModel viewModel)
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
