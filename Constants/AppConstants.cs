namespace MyActivity.Constants;

public static class AppConstants
{
    public const string AppName = "My Activity";
    public const string DatabaseFileName = "myactivity.db3";
    public const string SessionUserIdKey = "session.user_id";
    public const string NotificationPromptedKey = "notifications.prompted";

    // Resolved at runtime per platform - never hardcoded.
    public static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);

    public static class Routes
    {
        // Top-level pages / tabs (absolute)
        public const string Startup = "//startup";
        public const string Login = "//login";
        public const string Dashboard = "//dashboard";
        public const string Attendance = "//attendance";
        public const string Account = "//account";
        public const string Tools = "//tools";
        public const string More = "//more";

        // Pushed pages (relative)
        public const string Signup = "signup";
        public const string ResetPassword = "resetpassword";
        public const string AttendanceSettings = "attendancesettings";
        public const string AttendanceHistory = "attendancehistory";
        public const string Transaction = "transaction";
        public const string Transactions = "transactions";
        public const string CalcBasic = "calcbasic";
        public const string CalcEmi = "calcemi";
        public const string CalcSip = "calcsip";
        public const string CalcSwp = "calcswp";
        public const string Meetings = "meetings";
        public const string MeetingEdit = "meetingedit";
        public const string MeetingDetail = "meetingdetail";
        public const string Tasks = "tasks";
        public const string TaskEdit = "taskedit";
        public const string ChangePassword = "changepassword";
        public const string Back = "..";
    }

    public static class Themes
    {
        public const string System = "System";
        public const string Light = "Light";
        public const string Dark = "Dark";
    }

    public static class Messages
    {
        public const string SaveFailed = "Unable to save data. Please try again.";
        public const string LoadFailed = "Unable to load data. Please try again.";
        public const string AccountCreated = "Account created successfully. Please log in.";
        public const string PasswordReset = "Password updated. Please log in with your new password.";
    }
}
