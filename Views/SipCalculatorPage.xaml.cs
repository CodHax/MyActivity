using MyActivity.ViewModels;

namespace MyActivity.Views;

public partial class SipCalculatorPage : ContentPage
{
    public SipCalculatorPage(SipCalculatorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
