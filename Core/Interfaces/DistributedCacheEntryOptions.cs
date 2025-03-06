namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
/// Represents the options used by a distributed cache entry.
/// </summary>
public class DistributedCacheEntryOptions
{
    /// <summary>
    /// Gets or sets an absolute expiration date for the cache entry.
    /// </summary>
    public DateTimeOffset? AbsoluteExpiration { get; set; }
        
    /// <summary>
    /// Gets or sets an absolute expiration time, relative to now.
    /// </summary>
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
        
    /// <summary>
    /// Gets or sets how long a cache entry can be inactive (e.g. not accessed) before it will be removed.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }
}