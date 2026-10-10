using MyActivity.Constants;
using MyActivity.Views;

namespace MyActivity;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Pushed (non-tab) pages. Tab pages are declared in AppShell.xaml.
        Routing.RegisterRoute(AppConstants.Routes.Signup, typeof(SignupPage));
        Routing.RegisterRoute(AppConstants.Routes.ResetPassword, typeof(ResetPasswordPage));
        Routing.RegisterRoute(AppConstants.Routes.AttendanceSettings, typeof(AttendanceSettingsPage));
        Routing.RegisterRoute(AppConstants.Routes.AttendanceHistory, typeof(AttendanceHistoryPage));
        Routing.RegisterRoute(AppConstants.Routes.Transaction, typeof(TransactionEditPage));
        Routing.RegisterRoute(AppConstants.Routes.Transactions, typeof(TransactionHistoryPage));
        Routing.RegisterRoute(AppConstants.Routes.CalcBasic, typeof(BasicCalculatorPage));
        Routing.RegisterRoute(AppConstants.Routes.CalcEmi, typeof(EmiCalculatorPage));
        Routing.RegisterRoute(AppConstants.Routes.CalcSip, typeof(SipCalculatorPage));
        Routing.RegisterRoute(AppConstants.Routes.CalcSwp, typeof(SwpCalculatorPage));
        Routing.RegisterRoute(AppConstants.Routes.Meetings, typeof(MeetingsPage));
        Routing.RegisterRoute(AppConstants.Routes.MeetingEdit, typeof(MeetingEditPage));
        Routing.RegisterRoute(AppConstants.Routes.MeetingDetail, typeof(MeetingDetailPage));
        Routing.RegisterRoute(AppConstants.Routes.Tasks, typeof(TasksPage));
        Routing.RegisterRoute(AppConstants.Routes.TaskEdit, typeof(TaskEditPage));
        Routing.RegisterRoute(AppConstants.Routes.ChangePassword, typeof(ChangePasswordPage));
    }
}
