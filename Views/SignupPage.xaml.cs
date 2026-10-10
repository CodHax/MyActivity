using MyActivity.ViewModels;

namespace MyActivity.Views;

public partial class SignupPage : ContentPage
{
    public SignupPage(SignupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
