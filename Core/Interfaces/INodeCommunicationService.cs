namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
/// Defines methods for communication between nodes in a distributed system.
/// </summary>
public interface INodeCommunicationService
{
    /// <summary>
    /// Sends an event to another node.
    /// </summary>
    /// <param name="endpoint">The endpoint of the target node.</param>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="event">The event data.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendEventAsync<TEvent>(string endpoint, string eventName, TEvent @event) where TEvent : IEvent;
        
    /// <summary>
    /// Uploads a file to another node.
    /// </summary>
    /// <param name="endpoint">The endpoint of the target node.</param>
    /// <param name="storagePath">The storage path for the file.</param>
    /// <param name="fileStream">The file data stream.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UploadFileAsync(string endpoint, string storagePath, Stream fileStream);
        
    /// <summary>
    /// Gets a file from another node.
    /// </summary>
    /// <param name="endpoint">The endpoint of the target node.</param>
    /// <param name="storagePath">The storage path for the file.</param>
    /// <returns>A stream containing the file data.</returns>
    Task<Stream> GetFileAsync(string endpoint, string storagePath);
        
    /// <summary>
    /// Sends a delete request for a file to another node.
    /// </summary>
    /// <param name="endpoint">The endpoint of the target node.</param>
    /// <param name="storagePath">The storage path for the file to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteFileAsync(string endpoint, string storagePath);
}