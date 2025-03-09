using System.Windows.Controls;

namespace Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

/// <summary>
/// Interface for plugins that provide collaborative tools (whiteboard, file sharing, etc.)
/// </summary>
public interface ICollaborationPlugin : IPlugin
{
    /// <summary>
    /// Start a collaboration session
    /// </summary>
    /// <param name="channelId">The channel to start collaboration in</param>
    /// <returns>A control to display the collaboration UI</returns>
    Task<UserControl> StartCollaborationAsync(Guid channelId);
        
    /// <summary>
    /// Join an existing collaboration session
    /// </summary>
    /// <param name="sessionId">The ID of the session to join</param>
    /// <returns>A control to display the collaboration UI</returns>
    Task<UserControl> JoinCollaborationAsync(string sessionId);
        
    /// <summary>
    /// End a collaboration session
    /// </summary>
    /// <param name="sessionId">The ID of the session to end</param>
    Task EndCollaborationAsync(string sessionId);
}