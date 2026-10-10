using MyActivity.ViewModels;

namespace MyActivity.Views;

public partial class TaskEditPage : ContentPage
{
    private readonly TaskEditViewModel _viewModel;

    public TaskEditPage(TaskEditViewModel viewModel)
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
