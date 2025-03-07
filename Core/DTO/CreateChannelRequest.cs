using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.DTO;

public class CreateChannelRequest
{
    public string Name { get; set; }
    public ChannelType Type { get; set; }
}