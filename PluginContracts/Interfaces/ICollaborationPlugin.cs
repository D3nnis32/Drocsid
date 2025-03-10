
using Drocsid.HenrikDennis2025.PluginContracts.Models;

namespace Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

/// <summary>
/// Interface for plugins that provide collaborative tools (whiteboard, file sharing, etc.)
/// </summary>
public interface ICollaborationPlugin : IPlugin
{
    Task<UiComponent> StartCollaborationAsync(Guid channelId);
    Task<UiComponent> JoinCollaborationAsync(string sessionId);
    Task EndCollaborationAsync(string sessionId);
}