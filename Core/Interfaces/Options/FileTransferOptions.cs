namespace Drocsid.HenrikDennis2025.Core.Interfaces.Options;

/// <summary>
/// Configuration options for file transfer service
/// </summary>
public class FileTransferOptions
{
    /// <summary>
    /// Maximum number of concurrent transfers per node
    /// </summary>
    public int MaxConcurrentTransfersPerNode { get; set; } = 5;
        
    /// <summary>
    /// Timeout for file transfer operations
    /// </summary>
    public TimeSpan TransferTimeout { get; set; } = TimeSpan.FromMinutes(30);
        
    /// <summary>
    /// Maximum file size for direct transfer (larger files use chunked transfer)
    /// </summary>
    public long MaxDirectTransferSize { get; set; } = 100 * 1024 * 1024; // 100 MB
        
    /// <summary>
    /// Retry attempts for failed transfers
    /// </summary>
    public int RetryAttempts { get; set; } = 3;
        
    /// <summary>
    /// Delay between retry attempts
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(10);
}