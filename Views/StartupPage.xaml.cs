using MyActivity.ViewModels;

namespace MyActivity.Views;

public partial class StartupPage : ContentPage
{
    private readonly StartupViewModel _viewModel;

    public StartupPage(StartupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(600);               // brief splash; remove if undesired
        await _viewModel.InitializeAsync();
    }
}
