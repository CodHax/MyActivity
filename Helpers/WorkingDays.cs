namespace MyActivity.Helpers;

public static class WorkingDays
{
    public const int DefaultMask = 62; // Mon-Fri
    public static bool IsWorkingDay(int mask, DayOfWeek day) => (mask & (1 << (int)day)) != 0;
    public static int Set(int mask, DayOfWeek day, bool on) =>
        on ? mask | (1 << (int)day) : mask & ~(1 << (int)day);
}
