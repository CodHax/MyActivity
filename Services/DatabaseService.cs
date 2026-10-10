using Microsoft.Extensions.Logging;
using MyActivity.Constants;
using SQLite;

namespace MyActivity.Services;

public interface IDatabaseService
{
    /// <summary>Returns the shared, initialised and migrated connection.</summary>
    Task<SQLiteAsyncConnection> GetConnectionAsync();
}

public class DatabaseService : IDatabaseService
{
    private const int CurrentSchemaVersion = 4;

    private readonly ILogger<DatabaseService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private SQLiteAsyncConnection? _connection;

    public DatabaseService(ILogger<DatabaseService> logger) => _logger = logger;

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is not null) return _connection;

        await _initLock.WaitAsync();
        try
        {
            if (_connection is not null) return _connection;

            var options = new SQLiteConnectionString(
                AppConstants.DatabasePath,
                storeDateTimeAsTicks: true);   // DateTime stored as UTC ticks (INTEGER)

            var connection = new SQLiteAsyncConnection(options);
            await connection.ExecuteScalarAsync<string>("PRAGMA journal_mode = WAL;");
            await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
            await MigrateAsync(connection);

            _connection = connection;
            return _connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database initialisation failed");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Versioned migrations driven by PRAGMA user_version.
    /// Add a new "if (version &lt; N)" block per step (Steps 2-4 add their tables here).
    /// </summary>
    private async Task MigrateAsync(SQLiteAsyncConnection db)
    {
        var version = await db.ExecuteScalarAsync<int>("PRAGMA user_version;");
        if (version >= CurrentSchemaVersion) return;

        if (version < 1)
        {
            await db.RunInTransactionAsync(conn =>
            {
                foreach (var sql in SchemaV1) conn.Execute(sql);
                conn.Execute("PRAGMA user_version = 1;");
            });
            _logger.LogInformation("Database migrated to schema v1");
        }

        if (version < 2)
        {
            await db.RunInTransactionAsync(conn =>
            {
                foreach (var sql in SchemaV2) conn.Execute(sql);
                conn.Execute("PRAGMA user_version = 2;");
            });
            _logger.LogInformation("Database migrated to schema v2");
        }

        if (version < 3)
        {
            await db.RunInTransactionAsync(conn =>
            {
                foreach (var sql in SchemaV3) conn.Execute(sql);
                conn.Execute("PRAGMA user_version = 3;");
            });
            _logger.LogInformation("Database migrated to schema v3");
        }

        if (version < 4)
        {
            await db.RunInTransactionAsync(conn =>
            {
                foreach (var sql in SchemaV4) conn.Execute(sql);
                conn.Execute("PRAGMA user_version = 4;");
            });
            _logger.LogInformation("Database migrated to schema v4");
        }
    }

    private static readonly string[] SchemaV1 =
    {
        @"CREATE TABLE IF NOT EXISTS Users (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            EmployeeId   TEXT    NOT NULL COLLATE NOCASE,
            FullName     TEXT    NOT NULL,
            Email        TEXT    NOT NULL COLLATE NOCASE,
            PasswordHash TEXT    NOT NULL,
            CreatedAt    INTEGER NOT NULL,
            UpdatedAt    INTEGER NOT NULL,
            IsActive     INTEGER NOT NULL DEFAULT 1
        );",
        "CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_EmployeeId ON Users(EmployeeId);",
        "CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Email ON Users(Email);",

        @"CREATE TABLE IF NOT EXISTS AppSettings (
            Id                          INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId                      INTEGER NOT NULL,
            Theme                       TEXT    NOT NULL DEFAULT 'System',
            NotificationsEnabled        INTEGER NOT NULL DEFAULT 1,
            AttendanceRemindersEnabled  INTEGER NOT NULL DEFAULT 1,
            MeetingRemindersEnabled     INTEGER NOT NULL DEFAULT 1,
            TaskRemindersEnabled        INTEGER NOT NULL DEFAULT 1,
            SnoozeMinutes               INTEGER NOT NULL DEFAULT 5,
            CreatedAt                   INTEGER NOT NULL,
            UpdatedAt                   INTEGER NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
        );",
        "CREATE UNIQUE INDEX IF NOT EXISTS IX_AppSettings_UserId ON AppSettings(UserId);"
    };

    private static readonly string[] SchemaV2 =
    {
        "ALTER TABLE AppSettings ADD COLUMN CheckInMinutes    INTEGER NOT NULL DEFAULT 510;",
        "ALTER TABLE AppSettings ADD COLUMN CheckOutMinutes   INTEGER NOT NULL DEFAULT 1050;",
        "ALTER TABLE AppSettings ADD COLUMN WorkingDaysMask   INTEGER NOT NULL DEFAULT 62;",

        @"CREATE TABLE IF NOT EXISTS Attendance (
            Id             INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId         INTEGER NOT NULL,
            AttendanceDate TEXT    NOT NULL,
            AttendanceType TEXT    NOT NULL,
            CheckInTime    INTEGER NULL,
            CheckOutTime   INTEGER NULL,
            ResponseTime   INTEGER NOT NULL,
            Status         TEXT    NOT NULL,
            Source         TEXT    NOT NULL,
            CreatedAt      INTEGER NOT NULL,
            UpdatedAt      INTEGER NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
        );",
        "CREATE UNIQUE INDEX IF NOT EXISTS IX_Attendance_User_Date_Type ON Attendance(UserId, AttendanceDate, AttendanceType);",
        "CREATE INDEX IF NOT EXISTS IX_Attendance_UserId ON Attendance(UserId);",
        "CREATE INDEX IF NOT EXISTS IX_Attendance_Date ON Attendance(AttendanceDate);",

        @"CREATE TABLE IF NOT EXISTS NotificationSchedules (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId       INTEGER NOT NULL,
            Category     TEXT    NOT NULL,
            ReferenceKey TEXT    NOT NULL,
            FireAt       INTEGER NOT NULL,
            CreatedAt    INTEGER NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
        );",
        "CREATE UNIQUE INDEX IF NOT EXISTS IX_NotifSched_Unique ON NotificationSchedules(UserId, Category, ReferenceKey);",
        "CREATE INDEX IF NOT EXISTS IX_NotifSched_User ON NotificationSchedules(UserId);"
    };

    private static readonly string[] SchemaV3 =
    {
        @"CREATE TABLE IF NOT EXISTS AccountTransactions (
            Id              INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId          INTEGER NOT NULL,
            TransactionType TEXT    NOT NULL CHECK (TransactionType IN ('Credit','Debit')),
            AmountMinor     INTEGER NOT NULL CHECK (AmountMinor > 0),
            Category        TEXT    NOT NULL,
            Description     TEXT    NOT NULL,
            TransactionDate TEXT    NOT NULL,
            TransactionTime TEXT    NOT NULL,
            Notes           TEXT    NULL,
            CreatedAt       INTEGER NOT NULL,
            UpdatedAt       INTEGER NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
        );",
        "CREATE INDEX IF NOT EXISTS IX_AccTx_UserId ON AccountTransactions(UserId);",
        "CREATE INDEX IF NOT EXISTS IX_AccTx_User_Date ON AccountTransactions(UserId, TransactionDate);",
        "CREATE INDEX IF NOT EXISTS IX_AccTx_User_Category ON AccountTransactions(UserId, Category);"
    };

    private static readonly string[] SchemaV4 =
    {
        @"CREATE TABLE IF NOT EXISTS Meetings (
            Id              INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId          INTEGER NOT NULL,
            Title           TEXT    NOT NULL,
            Description     TEXT    NULL,
            MeetingDate     TEXT    NOT NULL,
            StartTime       TEXT    NOT NULL,
            EndTime         TEXT    NOT NULL,
            Location        TEXT    NULL,
            Participants    TEXT    NULL,
            Notes           TEXT    NULL,
            ReminderMinutes INTEGER NOT NULL DEFAULT -1,
            CreatedAt       INTEGER NOT NULL,
            UpdatedAt       INTEGER NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
        );",
        "CREATE INDEX IF NOT EXISTS IX_Meetings_User_Date ON Meetings(UserId, MeetingDate);",

        @"CREATE TABLE IF NOT EXISTS TaskItems (
            Id          INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId      INTEGER NOT NULL,
            Title       TEXT    NOT NULL,
            Description TEXT    NULL,
            TaskDate    TEXT    NULL,
            ReminderAt  INTEGER NULL,
            Priority    TEXT    NOT NULL DEFAULT 'Medium' CHECK (Priority IN ('Low','Medium','High')),
            Status      TEXT    NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending','Completed')),
            CompletedAt INTEGER NULL,
            CreatedAt   INTEGER NOT NULL,
            UpdatedAt   INTEGER NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
        );",
        "CREATE INDEX IF NOT EXISTS IX_Tasks_User_Status ON TaskItems(UserId, Status);",
        "CREATE INDEX IF NOT EXISTS IX_Tasks_User_Reminder ON TaskItems(UserId, ReminderAt);"
    };
}
