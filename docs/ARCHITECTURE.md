# Architecture

    Views (XAML)  ->  ViewModels (CommunityToolkit.Mvvm)  ->  Services (business rules)  ->  Repositories (SQL)  ->  SQLite
                                                           \->  NotificationService -> Plugin.LocalNotification (OS alarms)

- Models/                 Tables (User, AppSetting, Attendance, AccountTransaction, Meeting, TaskItem, NotificationSchedule)
- Repositories/           Only place that writes SQL. Every query is parameterised and filtered by UserId.
- Services/               Rules + validation (AuthenticationService, AttendanceService, AccountService, MeetingService,
                          TaskService, NotificationService, ReminderCoordinator, ThemeService, SessionService, SettingsService)
- ViewModels/             UI state and commands. No SQL, no platform code.
- Helpers/                Pure logic with no UI dependency (PasswordHasher, Validators, FinancialCalculators,
                          BasicCalculator, MoneyFormat) - these are unit-tested.
- Constants/              Routes, status names, notification ids.

## Database (schema version via PRAGMA user_version)
v1 Users, AppSettings | v2 Attendance, NotificationSchedules, schedule columns | v3 AccountTransactions | v4 Meetings, TaskItems
Every user-owned table has UserId with a foreign key (ON DELETE CASCADE) and an index.
Money is stored as integer paise. Dates are "yyyy-MM-dd" text, instants are UTC ticks.

## Notifications
- NotificationSchedules row Id == OS notification id; (UserId, Category, ReferenceKey) is UNIQUE -> no duplicates.
- Rescheduling always cancels the old id first (update = cancel + create).
- ReminderCoordinator rebuilds attendance (14 days), meetings (60 days) and task reminders on login, app start
  and settings change. Logout cancels everything for that user.
- Phone restart: the plugin restores its alarms (RECEIVE_BOOT_COMPLETED) and the app tops up on next open.
- Buttons: attendance (Present / Absent / Next 5 min), task (Mark complete / Snooze 10 min).

## Ready for later (not in V1)
Cloud sync, admin panel, backup/restore, reports: all data access goes through repositories/services, and each table
has CreatedAt/UpdatedAt, so a sync layer can be added without touching the UI.
