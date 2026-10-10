using MyActivity.ViewModels;

namespace MyActivity.Views;

public partial class MeetingDetailPage : ContentPage
{
    private readonly MeetingDetailViewModel _viewModel;

    public MeetingDetailPage(MeetingDetailViewModel viewModel)
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
