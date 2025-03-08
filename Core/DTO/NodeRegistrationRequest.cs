using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Drocsid.HenrikDennis2025.Core.DTO;

/// <summary>
/// Request model for node registration
/// </summary>
public class NodeRegistrationRequest
{
    private string _id = string.Empty;

    // Optional ID (if re-registering an existing node)
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string Id 
    { 
        get => _id; 
        set => _id = value ?? string.Empty; 
    }

    [Required]
    public string Hostname { get; set; }

    [Required]
    public string Endpoint { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public long AvailableStorage { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public long TotalStorage { get; set; }

    // Optional region for geographic awareness
    public string Region { get; set; }

    // Optional tags for node categorization
    public List<string> Tags { get; set; } = new();
}