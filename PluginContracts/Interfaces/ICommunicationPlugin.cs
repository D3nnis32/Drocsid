using Drocsid.HenrikDennis2025.PluginContracts.Models;

namespace Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

/// <summary>
/// Interface for plugins that provide communications features (voice, video, etc.)
/// </summary>
public interface ICommunicationPlugin : IPlugin
{
    IEnumerable<CommunicationMode> SupportedModes { get; }
        
    Task<UiComponent> StartSessionAsync(Guid channelId, CommunicationMode mode);
    Task<UiComponent> JoinSessionAsync(string sessionId);
    Task EndSessionAsync(string sessionId);
}