using System.Text.Json;

namespace DevTools.Ipc;

/// <summary>
/// Optional capability for <see cref="IBridgeRequestHandler"/> implementations that
/// broadcast progress notifications to connected pipe clients.
/// </summary>
public interface IBridgeNotificationPublisher
{
    Action<string, JsonElement?>? NotificationSender { get; set; }
}
