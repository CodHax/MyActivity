namespace MyActivity.Helpers;

/// <summary>Notification return-data format: "{prefix}|{userId}|{id}".</summary>
public static class NotificationPayload
{
    public static string Build(string prefix, int userId, int id) => $"{prefix}|{userId}|{id}";

    public static bool TryParse(string? payload, string prefix, out int userId, out int id)
    {
        userId = 0; id = 0;
        var parts = payload?.Split('|');
        return parts is { Length: 3 } && parts[0] == prefix
               && int.TryParse(parts[1], out userId) && int.TryParse(parts[2], out id);
    }
}
