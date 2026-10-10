using MyActivity.ViewModels;

namespace MyActivity.Views;

public partial class ToolsPage : ContentPage
{
    public ToolsPage(ToolsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
