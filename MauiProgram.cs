using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using MyActivity.Repositories;
using MyActivity.Services;
using MyActivity.ViewModels;
using MyActivity.Views;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.Core.Models.AppleOption;

namespace MyActivity;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseLocalNotification(config =>
            {
                // Buttons shown on every attendance notification.
                config.AddCategory(new NotificationCategory(NotificationCategoryType.Status)
                {
                    ActionList = new HashSet<NotificationAction>
                    {
                        new NotificationAction(AttendanceConstants.ActionPresent)
                        {
                            Title = "Present",
                            Android = { LaunchAppWhenTapped = false },
                            Apple = { Action = AppleActionType.Background }
                        },
                        new NotificationAction(AttendanceConstants.ActionAbsent)
                        {
                            Title = "Absent",
                            Android = { LaunchAppWhenTapped = false },
                            Apple = { Action = AppleActionType.Background }
                        },
                        new NotificationAction(AttendanceConstants.ActionSnooze)
                        {
                            Title = "Next 5 min",
                            Android = { LaunchAppWhenTapped = false },
                            Apple = { Action = AppleActionType.Background }
                        }
                    }
                });

                // Buttons shown on task reminders.
                config.AddCategory(new NotificationCategory(NotificationCategoryType.Reminder)
                {
                    ActionList = new HashSet<NotificationAction>
                    {
                        new NotificationAction(TaskConstants.ActionComplete)
                        {
                            Title = "Mark complete",
                            Android = { LaunchAppWhenTapped = false },
                            Apple = { Action = AppleActionType.Background }
                        },
                        new NotificationAction(TaskConstants.ActionSnooze)
                        {
                            Title = "Snooze 10 min",
                            Android = { LaunchAppWhenTapped = false },
                            Apple = { Action = AppleActionType.Background }
                        }
                    }
                });
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Data
        builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
        builder.Services.AddSingleton<IUserRepository, UserRepository>();
        builder.Services.AddSingleton<ISettingsRepository, SettingsRepository>();
        builder.Services.AddSingleton<IAttendanceRepository, AttendanceRepository>();
        builder.Services.AddSingleton<INotificationScheduleRepository, NotificationScheduleRepository>();
        builder.Services.AddSingleton<IAccountRepository, AccountRepository>();
        builder.Services.AddSingleton<IMeetingRepository, MeetingRepository>();
        builder.Services.AddSingleton<ITaskRepository, TaskRepository>();

        // Services
        builder.Services.AddSingleton<ISessionService, SessionService>();
        builder.Services.AddSingleton<INotificationService, NotificationService>();
        builder.Services.AddSingleton<IAttendanceService, AttendanceService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<IAccountService, AccountService>();
        builder.Services.AddSingleton<IMeetingService, MeetingService>();
        builder.Services.AddSingleton<ITaskService, TaskService>();
        builder.Services.AddSingleton<IReminderCoordinator, ReminderCoordinator>();
        builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<INotificationActionRouter, NotificationActionRouter>();

        // ViewModels + Views
        builder.Services.AddTransient<StartupViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<SignupViewModel>();
        builder.Services.AddTransient<ResetPasswordViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<AttendanceViewModel>();
        builder.Services.AddTransient<AttendanceSettingsViewModel>();
        builder.Services.AddTransient<AttendanceHistoryViewModel>();
        builder.Services.AddTransient<AccountViewModel>();
        builder.Services.AddTransient<TransactionEditViewModel>();
        builder.Services.AddTransient<TransactionHistoryViewModel>();
        builder.Services.AddTransient<BasicCalculatorViewModel>();
        builder.Services.AddTransient<EmiCalculatorViewModel>();
        builder.Services.AddTransient<SipCalculatorViewModel>();
        builder.Services.AddTransient<SwpCalculatorViewModel>();
        builder.Services.AddTransient<MeetingsViewModel>();
        builder.Services.AddTransient<MeetingEditViewModel>();
        builder.Services.AddTransient<MeetingDetailViewModel>();
        builder.Services.AddTransient<TasksViewModel>();
        builder.Services.AddTransient<TaskEditViewModel>();
        builder.Services.AddTransient<ToolsViewModel>();
        builder.Services.AddTransient<MoreViewModel>();
        builder.Services.AddTransient<ChangePasswordViewModel>();

        builder.Services.AddTransient<StartupPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<SignupPage>();
        builder.Services.AddTransient<ResetPasswordPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<AttendancePage>();
        builder.Services.AddTransient<AttendanceSettingsPage>();
        builder.Services.AddTransient<AttendanceHistoryPage>();
        builder.Services.AddTransient<AccountPage>();
        builder.Services.AddTransient<TransactionEditPage>();
        builder.Services.AddTransient<TransactionHistoryPage>();
        builder.Services.AddTransient<BasicCalculatorPage>();
        builder.Services.AddTransient<EmiCalculatorPage>();
        builder.Services.AddTransient<SipCalculatorPage>();
        builder.Services.AddTransient<SwpCalculatorPage>();
        builder.Services.AddTransient<MeetingsPage>();
        builder.Services.AddTransient<MeetingEditPage>();
        builder.Services.AddTransient<MeetingDetailPage>();
        builder.Services.AddTransient<TasksPage>();
        builder.Services.AddTransient<TaskEditPage>();
        builder.Services.AddTransient<ToolsPage>();
        builder.Services.AddTransient<MorePage>();
        builder.Services.AddTransient<ChangePasswordPage>();

        var app = builder.Build();

        // Subscribe here (not in a page) so a button press that starts the app process is not missed.
        var router = app.Services.GetRequiredService<INotificationActionRouter>();
        LocalNotificationCenter.Current.NotificationActionTapped += e =>
            router.OnActionTapped(e.ActionId, e.Request?.ReturningData, e.IsTapped, e.IsDismissed);

        return app;
    }
}
