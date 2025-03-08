namespace Drocsid.HenrikDennis2025.Core.Interfaces.Services;

/// <summary>
/// Interface for file transfer operations between storage nodes
/// </summary>
public interface IFileTransferService
{
    /// <summary>
    /// Transfers a file from a source node to a target node
    /// </summary>
    /// <param name="fileId">The ID of the file to transfer</param>
    /// <param name="sourceNodeId">Preferred source node ID (may use alternative if this is unavailable)</param>
    /// <param name="targetNodeId">Target node ID to copy the file to</param>
    /// <returns>True if transfer was successful, false otherwise</returns>
    Task<bool> TransferFileAsync(string fileId, string sourceNodeId, string targetNodeId);
}