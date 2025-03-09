using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;
using Drocsid.HenrikDennis2025.PluginContracts.Models;

namespace Drocsid.HenrikDennis2025.Core.Plugins;

/// <summary>
    /// Implementation of UI service
    /// </summary>
    public class UIService : IUIService
    {
        private readonly Dictionary<Guid, List<System.Windows.Controls.UserControl>> _channelHeaderComponents = new();
        
        private readonly List<System.Windows.Controls.UserControl> _sidebarComponents = new();
            
        private readonly IPluginLogger _logger;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public UIService(IPluginLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Register a UI component to be shown in the channel header
        /// </summary>
        public void RegisterChannelHeaderComponent(Guid channelId, System.Windows.Controls.UserControl component)
        {
            if (!_channelHeaderComponents.ContainsKey(channelId))
            {
                _channelHeaderComponents[channelId] = new List<System.Windows.Controls.UserControl>();
            }
            
            _channelHeaderComponents[channelId].Add(component);
            
            // Fire event to notify UI that components have changed
            ChannelHeaderComponentsChanged?.Invoke(this, channelId);
        }
        
        /// <summary>
        /// Register a UI component to be shown in the sidebar
        /// </summary>
        public void RegisterSidebarComponent(System.Windows.Controls.UserControl component)
        {
            _sidebarComponents.Add(component);
            
            // Fire event to notify UI that components have changed
            SidebarComponentsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Show a notification to the user
        /// </summary>
        public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
        {
            // Execute on UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // Create a simple notification (customize based on your UI)
                var color = type switch
                {
                    NotificationType.Success => System.Windows.Media.Colors.Green,
                    NotificationType.Warning => System.Windows.Media.Colors.Orange,
                    NotificationType.Error => System.Windows.Media.Colors.Red,
                    _ => System.Windows.Media.Colors.Blue
                };
                
                // Fire event to show notification
                NotificationRequested?.Invoke(this, new NotificationEventArgs
                {
                    Title = title,
                    Message = message,
                    Type = type
                });
            });
        }
        
        /// <summary>
        /// Show a confirmation dialog to the user
        /// </summary>
        public Task<bool> ShowConfirmationDialogAsync(string title, string message)
        {
            return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var result = System.Windows.MessageBox.Show(
                    message,
                    title,
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
                
                return result == System.Windows.MessageBoxResult.Yes;
            }).Task;
        }
        
        /// <summary>
        /// Show a modal window with plugin content
        /// </summary>
        public Task ShowModalAsync(string title, System.Windows.Controls.UserControl content)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // Create a new window
                    var window = new System.Windows.Window
                    {
                        Title = title,
                        Content = content,
                        SizeToContent = System.Windows.SizeToContent.WidthAndHeight,
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                        Owner = System.Windows.Application.Current.MainWindow,
                        MinWidth = 400,
                        MinHeight = 300
                    };
                    
                    // Close the task when the window is closed
                    window.Closed += (s, e) => tcs.TrySetResult(true);
                    
                    // Show the window
                    window.Show();
                }
                catch (Exception ex)
                {
                    _logger.Error("Error showing modal window", ex);
                    tcs.TrySetException(ex);
                }
            });
            
            return tcs.Task;
        }
        
        /// <summary>
        /// Get all channel header components for a channel
        /// </summary>
        public IEnumerable<System.Windows.Controls.UserControl> GetChannelHeaderComponents(Guid channelId)
        {
            if (_channelHeaderComponents.TryGetValue(channelId, out var components))
            {
                return components;
            }
            
            return Enumerable.Empty<System.Windows.Controls.UserControl>();
        }
        
        /// <summary>
        /// Get all sidebar components
        /// </summary>
        public IEnumerable<System.Windows.Controls.UserControl> GetSidebarComponents()
        {
            return _sidebarComponents;
        }
        
        /// <summary>
        /// Event raised when channel header components change
        /// </summary>
        public event EventHandler<Guid> ChannelHeaderComponentsChanged;
        
        /// <summary>
        /// Event raised when sidebar components change
        /// </summary>
        public event EventHandler SidebarComponentsChanged;
        
        /// <summary>
        /// Event raised when a notification should be shown
        /// </summary>
        public event EventHandler<NotificationEventArgs> NotificationRequested;
    }