using MyActivity.ViewModels;

namespace MyActivity.Views;

public partial class SwpCalculatorPage : ContentPage
{
    public SwpCalculatorPage(SwpCalculatorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
