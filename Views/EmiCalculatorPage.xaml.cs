using MyActivity.ViewModels;

namespace MyActivity.Views;

public partial class EmiCalculatorPage : ContentPage
{
    public EmiCalculatorPage(EmiCalculatorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
