namespace Drocsid.HenrikDennis2025.PluginContracts.Models;

/// <summary>
/// Basic channel information for plugins
/// </summary>
public class ChannelInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public List<Guid> MemberIds { get; set; }
}