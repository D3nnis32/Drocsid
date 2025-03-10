namespace Drocsid.HenrikDennis2025.PluginContracts.Models;

/// <summary>
/// A cross-platform abstraction for UI components to replace WPF-specific UserControl
/// </summary>
public class UiComponent
{
    /// <summary>
    /// Unique identifier for the component
    /// </summary>
    public string Id { get; set; }
    
    /// <summary>
    /// Component type (can be used for serialization/deserialization)
    /// </summary>
    public string ComponentType { get; set; }
    
    /// <summary>
    /// Component configuration data (serialized as JSON)
    /// </summary>
    public string Configuration { get; set; }
    
    /// <summary>
    /// Additional component properties
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
}