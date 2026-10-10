using MyActivity.ViewModels;

namespace MyActivity.Views;

public partial class MeetingEditPage : ContentPage
{
    private readonly MeetingEditViewModel _viewModel;

    public MeetingEditPage(MeetingEditViewModel viewModel)
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
