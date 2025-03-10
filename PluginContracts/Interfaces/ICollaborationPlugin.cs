using System.Windows.Controls;

namespace Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

/// <summary>
/// Interface for plugins that provide collaborative tools (whiteboard, file sharing, etc.)
/// </summary>
public interface ICollaborationPlugin : IPlugin
{
    Task<object> StartCollaborationAsync(Guid channelId);
    Task<object> JoinCollaborationAsync(string sessionId);
    Task EndCollaborationAsync(string sessionId);
}