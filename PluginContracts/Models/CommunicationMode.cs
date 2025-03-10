namespace Drocsid.HenrikDennis2025.PluginContracts.Models;

/// <summary>
/// Modes of communication a plugin can support
/// </summary>
public enum CommunicationMode
{
    /// <summary>
    /// Audio-only communication
    /// </summary>
    Audio,
        
    /// <summary>
    /// Video and audio communication
    /// </summary>
    Video,
        
    /// <summary>
    /// Screen sharing
    /// </summary>
    ScreenSharing
}