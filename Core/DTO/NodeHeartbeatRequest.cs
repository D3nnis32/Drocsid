using System.ComponentModel.DataAnnotations;
using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.DTO;

// Request model for node heartbeat
public class NodeHeartbeatRequest
{
    [Required]
    public NodeStatus Status { get; set; }
}