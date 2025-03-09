using System.Windows.Controls;
using Drocsid.HenrikDennis2025.PluginContracts.Models;

namespace Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

/// <summary>
/// Interface for plugins that provide communications features (voice, video, etc.)
/// </summary>
public interface ICommunicationPlugin : IPlugin
{
    /// <summary>
    /// Supported communication modes (audio, video, etc.)
    /// </summary>
    IEnumerable<CommunicationMode> SupportedModes { get; }
        
    /// <summary>
    /// Start a communication session
    /// </summary>
    /// <param name="channelId">The channel to start communication in</param>
    /// <param name="mode">The communication mode to use</param>
    /// <returns>A control to display the communication UI</returns>
    Task<UserControl> StartSessionAsync(Guid channelId, CommunicationMode mode);
        
    /// <summary>
    /// Join an existing communication session
    /// </summary>
    /// <param name="sessionId">The ID of the session to join</param>
    /// <returns>A control to display the communication UI</returns>
    Task<UserControl> JoinSessionAsync(string sessionId);
        
    /// <summary>
    /// End a communication session
    /// </summary>
    /// <param name="sessionId">The ID of the session to end</param>
    Task EndSessionAsync(string sessionId);
}