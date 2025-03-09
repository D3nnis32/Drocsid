using Drocsid.HenrikDennis2025.PluginContracts.Models;

namespace Drocsid.HenrikDennis2025.Core.Plugins;

/// <summary>
/// Event args for notifications
/// </summary>
public class NotificationEventArgs : EventArgs
{
    public string Title { get; set; }
    public string Message { get; set; }
    public NotificationType Type { get; set; }
}