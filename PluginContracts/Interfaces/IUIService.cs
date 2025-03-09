using System.Windows.Controls;
using Drocsid.HenrikDennis2025.PluginContracts.Models;

namespace Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

/// <summary>
/// Interface for interacting with the UI
/// </summary>
public interface IUIService
{
    /// <summary>
    /// Register a UI component to be shown in the channel header
    /// </summary>
    void RegisterChannelHeaderComponent(Guid channelId, UserControl component);
        
    /// <summary>
    /// Register a UI component to be shown in the sidebar
    /// </summary>
    void RegisterSidebarComponent(UserControl component);
        
    /// <summary>
    /// Show a notification to the user
    /// </summary>
    void ShowNotification(string title, string message, NotificationType type = NotificationType.Info);
        
    /// <summary>
    /// Show a dialog to the user
    /// </summary>
    Task<bool> ShowConfirmationDialogAsync(string title, string message);
        
    /// <summary>
    /// Show a modal window with plugin content
    /// </summary>
    Task ShowModalAsync(string title, UserControl content);
}