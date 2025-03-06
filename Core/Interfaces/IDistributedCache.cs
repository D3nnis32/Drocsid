namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
    /// Represents a distributed cache of serialized values.
    /// </summary>
    public interface IDistributedCache
    {
        /// <summary>
        /// Gets a value from the cache with the specified key.
        /// </summary>
        /// <param name="key">The key to get the cached value for.</param>
        /// <param name="token">A token to cancel the operation.</param>
        /// <returns>The cached value, or null if the key was not found.</returns>
        Task<byte[]> GetAsync(string key, CancellationToken token = default);
        
        /// <summary>
        /// Gets a string value from the cache with the specified key.
        /// </summary>
        /// <param name="key">The key to get the cached value for.</param>
        /// <param name="token">A token to cancel the operation.</param>
        /// <returns>The cached value, or null if the key was not found.</returns>
        Task<string> GetStringAsync(string key, CancellationToken token = default);
        
        /// <summary>
        /// Sets a value in the cache with the specified key.
        /// </summary>
        /// <param name="key">The key to store the value under.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="options">The cache options for the value.</param>
        /// <param name="token">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options = null, CancellationToken token = default);
        
        /// <summary>
        /// Sets a string value in the cache with the specified key.
        /// </summary>
        /// <param name="key">The key to store the value under.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="options">The cache options for the value.</param>
        /// <param name="token">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetStringAsync(string key, string value, DistributedCacheEntryOptions options = null, CancellationToken token = default);
        
        /// <summary>
        /// Refreshes a value in the cache, resetting its sliding expiration timeout.
        /// </summary>
        /// <param name="key">The key to refresh.</param>
        /// <param name="token">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RefreshAsync(string key, CancellationToken token = default);
        
        /// <summary>
        /// Removes a value from the cache.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <param name="token">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RemoveAsync(string key, CancellationToken token = default);
    }