using MyActivity.ViewModels;

namespace MyActivity.Views;

public partial class BasicCalculatorPage : ContentPage
{
    public BasicCalculatorPage(BasicCalculatorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
