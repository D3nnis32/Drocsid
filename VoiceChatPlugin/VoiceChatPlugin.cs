using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Drocsid.HenrikDennis2025.PluginContracts;
using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;
using Drocsid.HenrikDennis2025.PluginContracts.Models;

namespace VoiceChatPlugin
{
    /// <summary>
    /// Implements a voice chat plugin for Drocsid
    /// </summary>
    public class VoiceChatPlugin : ICommunicationPlugin
    {
        private IPluginContext _context;
        private bool _initialized;
        private readonly Dictionary<string, VoiceChatSession> _activeSessions = new Dictionary<string, VoiceChatSession>();
        
        // Plugin metadata
        public string Id => "com.example.voicechat";
        public string Name => "Voice Chat";
        public string Description => "Adds voice chat capabilities to channels";
        public Version Version => new Version(1, 0, 0);
        public string Author => "Example Developer";
        public string InfoUrl => "https://example.com/voicechat";
        public PluginState State { get; private set; } = PluginState.Uninitialized;
        
        // Communication modes supported by this plugin
        public IEnumerable<CommunicationMode> SupportedModes => new[] 
        { 
            CommunicationMode.Audio, 
            CommunicationMode.ScreenSharing 
        };
        
        // Plugin icon
        public ImageSource Icon
        {
            get
            {
                // Create a simple icon for this plugin
                // In a real plugin, you would load this from resources
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawEllipse(
                        Brushes.DodgerBlue, 
                        new Pen(Brushes.White, 1), 
                        new Point(8, 8), 
                        8, 8);
                    
                    // Draw a microphone icon
                    var geometry = Geometry.Parse("M8,1 L8,15 M4,5 L12,5 M4,15 L12,15");
                    drawingContext.DrawGeometry(null, new Pen(Brushes.White, 2), geometry);
                }
                
                var bitmap = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(drawingVisual);
                bitmap.Freeze();
                
                return bitmap;
            }
        }
        
        /// <summary>
        /// Initialize the plugin
        /// </summary>
        public async Task InitializeAsync(IPluginContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            
            // Log initialization
            _context.Logger.Info("Initializing Voice Chat plugin...");
            
            try
            {
                // Register UI components
                RegisterUIComponents();
                
                // Initialize audio system
                await InitializeAudioSystemAsync();
                
                // Subscribe to events
                SubscribeToEvents();
                
                // Set state to running
                State = PluginState.Running;
                _initialized = true;
                
                _context.Logger.Info("Voice Chat plugin initialized successfully");
            }
            catch (Exception ex)
            {
                _context.Logger.Error("Error initializing Voice Chat plugin", ex);
                State = PluginState.Error;
                throw;
            }
        }
        
        /// <summary>
        /// Shut down the plugin
        /// </summary>
        public async Task ShutdownAsync()
        {
            if (!_initialized)
                return;
                
            _context.Logger.Info("Shutting down Voice Chat plugin...");
            
            try
            {
                // End all active sessions
                foreach (var session in _activeSessions.Values)
                {
                    await EndSessionInternalAsync(session.SessionId);
                }
                
                // Unregister UI components
                // In a real plugin, you would remove any UI components you registered
                
                // Unsubscribe from events
                UnsubscribeFromEvents();
                
                // Shut down audio system
                await ShutdownAudioSystemAsync();
                
                _initialized = false;
                State = PluginState.Uninitialized;
                
                _context.Logger.Info("Voice Chat plugin shut down successfully");
            }
            catch (Exception ex)
            {
                _context.Logger.Error("Error shutting down Voice Chat plugin", ex);
                State = PluginState.Error;
                throw;
            }
        }
        
        /// <summary>
        /// Get plugin settings view
        /// </summary>
        public UserControl GetSettingsView()
        {
            return new VoiceChatSettingsControl(_context);
        }
        
        /// <summary>
        /// Start a communication session
        /// </summary>
        public async Task<UserControl> StartSessionAsync(Guid channelId, CommunicationMode mode)
        {
            if (!_initialized)
                throw new InvalidOperationException("Plugin is not initialized");
                
            if (!SupportedModes.Contains(mode))
                throw new ArgumentException($"Unsupported communication mode: {mode}");
                
            _context.Logger.Info($"Starting {mode} session in channel {channelId}");
            
            try
            {
                // Get channel info
                var channelInfo = await _context.UserSession.GetChannelInfoAsync(channelId);
                
                // Create session ID
                string sessionId = $"{channelId}-{Guid.NewGuid()}";
                
                // Create a new session
                var session = new VoiceChatSession
                {
                    SessionId = sessionId,
                    ChannelId = channelId,
                    Mode = mode,
                    ChannelName = channelInfo.Name,
                    StartTime = DateTime.UtcNow,
                    Participants = new List<Guid> { _context.UserSession.CurrentUserId }
                };
                
                // Store the session
                _activeSessions[sessionId] = session;
                
                // Create UI
                var sessionControl = new VoiceChatSessionControl(session, mode, _context);
                
                // Add current user to participants
                session.Participants.Add(_context.UserSession.CurrentUserId);
                
                // In a real plugin, you would initialize audio/video hardware here
                await Task.Delay(500); // Simulate initialization
                
                // Notify other users that a session has started
                _context.EventManager.Publish("VoiceChat.SessionStarted", new SessionStartedEvent
                {
                    SessionId = sessionId,
                    ChannelId = channelId,
                    Mode = mode,
                    InitiatorId = _context.UserSession.CurrentUserId
                });
                
                _context.Logger.Info($"Started {mode} session {sessionId} in channel {channelId}");
                
                // Return the session UI
                return sessionControl;
            }
            catch (Exception ex)
            {
                _context.Logger.Error($"Error starting {mode} session in channel {channelId}", ex);
                throw;
            }
        }
        
        /// <summary>
        /// Join an existing communication session
        /// </summary>
        public async Task<UserControl> JoinSessionAsync(string sessionId)
        {
            if (!_initialized)
                throw new InvalidOperationException("Plugin is not initialized");
                
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentNullException(nameof(sessionId));
                
            _context.Logger.Info($"Joining session {sessionId}");
            
            try
            {
                // In a real plugin, you would retrieve session info from the server
                // For this example, we'll create a dummy session
                
                // Parse the session ID to get the channel ID
                var parts = sessionId.Split('-');
                if (parts.Length < 2 || !Guid.TryParse(parts[0], out var channelId))
                {
                    throw new ArgumentException("Invalid session ID format");
                }
                
                // Get channel info
                var channelInfo = await _context.UserSession.GetChannelInfoAsync(channelId);
                
                // Create a new session
                var session = new VoiceChatSession
                {
                    SessionId = sessionId,
                    ChannelId = channelId,
                    Mode = CommunicationMode.Audio, // Assume audio by default
                    ChannelName = channelInfo.Name,
                    StartTime = DateTime.UtcNow.AddMinutes(-1), // Assume started 1 minute ago
                    Participants = new List<Guid> { _context.UserSession.CurrentUserId }
                };
                
                // Store the session
                _activeSessions[sessionId] = session;
                
                // Create UI
                var sessionControl = new VoiceChatSessionControl(session, session.Mode, _context);
                
                // In a real plugin, you would connect to the session here
                await Task.Delay(500); // Simulate connection
                
                // Notify that user has joined
                _context.EventManager.Publish("VoiceChat.UserJoined", new UserJoinedEvent
                {
                    SessionId = sessionId,
                    UserId = _context.UserSession.CurrentUserId
                });
                
                _context.Logger.Info($"Joined session {sessionId}");
                
                // Return the session UI
                return sessionControl;
            }
            catch (Exception ex)
            {
                _context.Logger.Error($"Error joining session {sessionId}", ex);
                throw;
            }
        }
        
        /// <summary>
        /// End a communication session
        /// </summary>
        public async Task EndSessionAsync(string sessionId)
        {
            await EndSessionInternalAsync(sessionId);
        }
        
        #region Private Methods
        
        /// <summary>
        /// Register UI components
        /// </summary>
        private void RegisterUIComponents()
        {
            // Show a button in the channel header to start a voice chat
            MediaTypeNames.Application.Current.Dispatcher.Invoke(() =>
            {
                var button = new Button
                {
                    Content = "Voice Chat",
                    Margin = new Thickness(5),
                    Padding = new Thickness(5, 2, 5, 2)
                };
                
                button.Click += async (sender, e) =>
                {
                    try
                    {
                        // Get the current channel from the sender (assuming the button is in a channel header)
                        var channelId = Guid.Parse((string)((Button)sender).Tag);
                        
                        // Ask the user which mode to use
                        var result = MessageBox.Show(
                            "Do you want to start a voice call?\n\nYes = Audio only\nNo = Cancel",
                            "Start Voice Chat",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                            
                        if (result == MessageBoxResult.Yes)
                        {
                            // Start an audio session
                            var control = await StartSessionAsync(channelId, CommunicationMode.Audio);
                            
                            // Show the control in a window
                            _context.UIService.ShowModalAsync("Voice Chat", control);
                        }
                    }
                    catch (Exception ex)
                    {
                        _context.Logger.Error("Error starting voice chat from UI", ex);
                        MessageBox.Show(
                            $"Error starting voice chat: {ex.Message}",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                };
                
                // This is a placeholder for demonstration
                // In a real application, you would add this button to the channel header
            });
        }
        
        /// <summary>
        /// Initialize the audio system
        /// </summary>
        private async Task InitializeAudioSystemAsync()
        {
            // In a real plugin, you would initialize audio hardware, codecs, etc.
            // For this example, we'll just simulate initialization
            await Task.Delay(100);
        }
        
        /// <summary>
        /// Shut down the audio system
        /// </summary>
        private async Task ShutdownAudioSystemAsync()
        {
            // In a real plugin, you would shut down audio hardware, codecs, etc.
            // For this example, we'll just simulate shutdown
            await Task.Delay(100);
        }
        
        /// <summary>
        /// Subscribe to events
        /// </summary>
        private void SubscribeToEvents()
        {
            // Subscribe to session invitation events
            _context.EventManager.Subscribe<SessionInvitationEvent>(
                "VoiceChat.SessionInvitation",
                OnSessionInvitation);
                
            // Subscribe to user joined events
            _context.EventManager.Subscribe<UserJoinedEvent>(
                "VoiceChat.UserJoined",
                OnUserJoined);
                
            // Subscribe to user left events
            _context.EventManager.Subscribe<UserLeftEvent>(
                "VoiceChat.UserLeft",
                OnUserLeft);
        }
        
        /// <summary>
        /// Unsubscribe from events
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            // Unsubscribe from events
            _context.EventManager.Unsubscribe<SessionInvitationEvent>(
                "VoiceChat.SessionInvitation",
                OnSessionInvitation);
                
            _context.EventManager.Unsubscribe<UserJoinedEvent>(
                "VoiceChat.UserJoined",
                OnUserJoined);
                
            _context.EventManager.Unsubscribe<UserLeftEvent>(
                "VoiceChat.UserLeft",
                OnUserLeft);
        }
        
        /// <summary>
        /// Handle session invitation events
        /// </summary>
        private void OnSessionInvitation(SessionInvitationEvent evt)
        {
            // Check if we're already in this session
            if (_activeSessions.ContainsKey(evt.SessionId))
            {
                return;
            }
            
            // Show a notification to the user
            MediaTypeNames.Application.Current.Dispatcher.Invoke(async () =>
            {
                // Get the initiator's user info
                var initiator = await _context.UserSession.GetUserInfoAsync(evt.InitiatorId);
                
                // Get the channel info
                var channel = await _context.UserSession.GetChannelInfoAsync(evt.ChannelId);
                
                // Show a notification
                var result = MessageBox.Show(
                    $"{initiator.Username} has started a {evt.Mode} session in channel {channel.Name}.\n\nDo you want to join?",
                    "Voice Chat Invitation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                    
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Join the session
                        var control = await JoinSessionAsync(evt.SessionId);
                        
                        // Show the control in a window
                        _context.UIService.ShowModalAsync("Voice Chat", control);
                    }
                    catch (Exception ex)
                    {
                        _context.Logger.Error($"Error joining session {evt.SessionId}", ex);
                        MessageBox.Show(
                            $"Error joining voice chat: {ex.Message}",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            });
        }
        
        /// <summary>
        /// Handle user joined events
        /// </summary>
        private void OnUserJoined(UserJoinedEvent evt)
        {
            // Update the session if we have it
            if (_activeSessions.TryGetValue(evt.SessionId, out var session))
            {
                if (!session.Participants.Contains(evt.UserId))
                {
                    session.Participants.Add(evt.UserId);
                    
                    // Log the user joining
                    _context.Logger.Info($"User {evt.UserId} joined session {evt.SessionId}");
                    
                    // Update UI
                    MediaTypeNames.Application.Current.Dispatcher.Invoke(async () =>
                    {
                        // Get user info
                        var user = await _context.UserSession.GetUserInfoAsync(evt.UserId);
                        
                        // Show a notification
                        _context.UIService.ShowNotification(
                            "User Joined",
                            $"{user.Username} has joined the voice chat",
                            NotificationType.Info);
                    });
                }
            }
        }
        
        /// <summary>
        /// Handle user left events
        /// </summary>
        private void OnUserLeft(UserLeftEvent evt)
        {
            // Update the session if we have it
            if (_activeSessions.TryGetValue(evt.SessionId, out var session))
            {
                if (session.Participants.Contains(evt.UserId))
                {
                    session.Participants.Remove(evt.UserId);
                    
                    // Log the user leaving
                    _context.Logger.Info($"User {evt.UserId} left session {evt.SessionId}");
                    
                    // Update UI
                    MediaTypeNames.Application.Current.Dispatcher.Invoke(async () =>
                    {
                        // Get user info
                        var user = await _context.UserSession.GetUserInfoAsync(evt.UserId);
                        
                        // Show a notification
                        _context.UIService.ShowNotification(
                            "User Left",
                            $"{user.Username} has left the voice chat",
                            NotificationType.Info);
                    });
                }
            }
        }
        
        /// <summary>
        /// Internal method to end a session
        /// </summary>
        private async Task EndSessionInternalAsync(string sessionId)
        {
            if (!_initialized)
                return;
                
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentNullException(nameof(sessionId));
                
            _context.Logger.Info($"Ending session {sessionId}");
            
            try
            {
                // Get the session
                if (!_activeSessions.TryGetValue(sessionId, out var session))
                {
                    _context.Logger.Warning($"Session {sessionId} not found");
                    return;
                }
                
                // Notify other users that the session has ended
                _context.EventManager.Publish("VoiceChat.UserLeft", new UserLeftEvent
                {
                    SessionId = sessionId,
                    UserId = _context.UserSession.CurrentUserId
                });
                
                // In a real plugin, you would disconnect from the session here
                await Task.Delay(100); // Simulate disconnection
                
                // Remove the session
                _activeSessions.Remove(sessionId);
                
                _context.Logger.Info($"Ended session {sessionId}");
            }
            catch (Exception ex)
            {
                _context.Logger.Error($"Error ending session {sessionId}", ex);
                throw;
            }
        }
        
        #endregion
    }
    
    #region Helper Classes
    
    /// <summary>
    /// Represents a voice chat session
    /// </summary>
    public class VoiceChatSession
    {
        public string SessionId { get; set; }
        public Guid ChannelId { get; set; }
        public string ChannelName { get; set; }
        public CommunicationMode Mode { get; set; }
        public DateTime StartTime { get; set; }
        public List<Guid> Participants { get; set; } = new List<Guid>();
    }
    
    /// <summary>
    /// Control for voice chat settings
    /// </summary>
    public class VoiceChatSettingsControl : UserControl
    {
        private readonly IPluginContext _context;
        
        public VoiceChatSettingsControl(IPluginContext context)
        {
            _context = context;
            
            // Create a simple settings UI
            var panel = new StackPanel { Margin = new Thickness(10) };
            
            // Title
            panel.Children.Add(new TextBlock 
            { 
                Text = "Voice Chat Settings", 
                FontSize = 16, 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });
            
            // Input device selection
            panel.Children.Add(new TextBlock { Text = "Input Device:", Margin = new Thickness(0, 5, 0, 0) });
            panel.Children.Add(new ComboBox 
            { 
                ItemsSource = new[] { "Default Device", "Microphone (USB)", "Microphone (Built-in)" },
                SelectedIndex = 0
            });
            
            // Output device selection
            panel.Children.Add(new TextBlock { Text = "Output Device:", Margin = new Thickness(0, 10, 0, 0) });
            panel.Children.Add(new ComboBox 
            { 
                ItemsSource = new[] { "Default Device", "Speakers (USB)", "Speakers (Built-in)" },
                SelectedIndex = 0
            });
            
            // Volume slider
            panel.Children.Add(new TextBlock { Text = "Volume:", Margin = new Thickness(0, 10, 0, 0) });
            panel.Children.Add(new Slider { Minimum = 0, Maximum = 100, Value = 75, TickFrequency = 10, TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight });
            
            // Enable noise cancellation
            panel.Children.Add(new CheckBox { Content = "Enable noise cancellation", IsChecked = true, Margin = new Thickness(0, 10, 0, 0) });
            
            // Echo cancellation
            panel.Children.Add(new CheckBox { Content = "Enable echo cancellation", IsChecked = true, Margin = new Thickness(0, 5, 0, 0) });
            
            // Push to talk
            panel.Children.Add(new CheckBox { Content = "Use push-to-talk", IsChecked = false, Margin = new Thickness(0, 5, 0, 0) });
            
            // Keybinding
            var keybindingPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            keybindingPanel.Children.Add(new TextBlock { Text = "Push-to-talk key:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            keybindingPanel.Children.Add(new TextBox { Text = "CTRL", Width = 100 });
            panel.Children.Add(keybindingPanel);
            
            // Save button
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            var saveButton = new Button { Content = "Save", Padding = new Thickness(10, 3, 10, 3) };
            saveButton.Click += (s, e) => MessageBox.Show("Settings saved!", "Voice Chat Plugin", MessageBoxButton.OK, MessageBoxImage.Information);
            buttonPanel.Children.Add(saveButton);
            panel.Children.Add(buttonPanel);
            
            Content = panel;
        }
    }
    
    /// <summary>
    /// Control for a voice chat session
    /// </summary>
    public class VoiceChatSessionControl : UserControl
    {
        private readonly VoiceChatSession _session;
        private readonly CommunicationMode _mode;
        private readonly IPluginContext _context;
        private readonly DispatcherTimer _timer;
        private readonly TextBlock _timeLabel;
        private readonly StackPanel _participantsPanel;
        
        public VoiceChatSessionControl(VoiceChatSession session, CommunicationMode mode, IPluginContext context)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _mode = mode;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            
            // Create UI
            var mainPanel = new Grid { Margin = new Thickness(10) };
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // Header
            var headerPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            
            headerPanel.Children.Add(new TextBlock 
            { 
                Text = $"{mode} Session - {session.ChannelName}", 
                FontSize = 16, 
                FontWeight = FontWeights.Bold 
            });
            
            _timeLabel = new TextBlock 
            { 
                Text = "00:00:00", 
                Margin = new Thickness(0, 5, 0, 0) 
            };
            headerPanel.Children.Add(_timeLabel);
            
            mainPanel.Children.Add(headerPanel);
            Grid.SetRow(headerPanel, 0);
            
            // Participants
            var participantsCard = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            var participantsScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 200
            };
            
            _participantsPanel = new StackPanel();
            participantsScrollViewer.Content = _participantsPanel;
            participantsCard.Child = participantsScrollViewer;
            
            mainPanel.Children.Add(participantsCard);
            Grid.SetRow(participantsCard, 1);
            
            // Controls
            var controlsPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };
            
            var muteButton = new ToggleButton 
            { 
                Content = "Mute", 
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(5)
            };
            
            var endButton = new Button 
            { 
                Content = "End Call", 
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(5),
                Background = Brushes.IndianRed,
                Foreground = Brushes.White
            };
            
            controlsPanel.Children.Add(muteButton);
            controlsPanel.Children.Add(endButton);
            
            if (mode == CommunicationMode.ScreenSharing)
            {
                var shareButton = new ToggleButton 
                { 
                    Content = "Share Screen", 
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(5)
                };
                controlsPanel.Children.Add(shareButton);
            }
            
            mainPanel.Children.Add(controlsPanel);
            Grid.SetRow(controlsPanel, 2);
            
            // Set up event handlers
            muteButton.Checked += (s, e) => OnMuteToggled(true);
            muteButton.Unchecked += (s, e) => OnMuteToggled(false);
            
            endButton.Click += async (s, e) => 
            {
                try
                {
                    // Confirm ending the call
                    var result = MessageBox.Show(
                        "Are you sure you want to end this call?",
                        "End Call",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                        
                    if (result == MessageBoxResult.Yes)
                    {
                        // End the session
                        await context.EventManager.GetPlugin<VoiceChatPlugin>().EndSessionAsync(_session.SessionId);
                        
                        // Close the window
                        var window = Window.GetWindow(this);
                        if (window != null)
                        {
                            window.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    context.Logger.Error("Error ending call", ex);
                    MessageBox.Show(
                        $"Error ending call: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };
            
            // Set up timer for call duration
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            
            _timer.Tick += (s, e) => UpdateCallDuration();
            _timer.Start();
            
            // Initialize participants list
            UpdateParticipantsList();
            
            Content = mainPanel;
        }
        
        /// <summary>
        /// Handle mute toggled
        /// </summary>
        private void OnMuteToggled(bool isMuted)
        {
            // In a real plugin, you would mute/unmute the audio here
            _context.Logger.Info($"Microphone {(isMuted ? "muted" : "unmuted")}");
        }
        
        /// <summary>
        /// Update call duration
        /// </summary>
        private void UpdateCallDuration()
        {
            var duration = DateTime.UtcNow - _session.StartTime;
            _timeLabel.Text = $"{duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        }
        
        /// <summary>
        /// Update participants list
        /// </summary>
        private async void UpdateParticipantsList()
        {
            try
            {
                _participantsPanel.Children.Clear();
                
                // Add a header
                _participantsPanel.Children.Add(new TextBlock 
                { 
                    Text = "Participants", 
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                });
                
                // Add each participant
                foreach (var participantId in _session.Participants)
                {
                    try
                    {
                        // Get user info
                        var user = await _context.UserSession.GetUserInfoAsync(participantId);
                        
                        // Create participant entry
                        var participantPanel = new StackPanel 
                        { 
                            Orientation = Orientation.Horizontal,
                            Margin = new Thickness(0, 2, 0, 2)
                        };
                        
                        // Status indicator
                        var statusIndicator = new Ellipse 
                        { 
                            Width = 10, 
                            Height = 10, 
                            Fill = Brushes.Green,
                            Margin = new Thickness(0, 0, 5, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        
                        // Username
                        var usernameLabel = new TextBlock 
                        { 
                            Text = user.Username,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        
                        participantPanel.Children.Add(statusIndicator);
                        participantPanel.Children.Add(usernameLabel);
                        
                        _participantsPanel.Children.Add(participantPanel);
                    }
                    catch (Exception ex)
                    {
                        _context.Logger.Error($"Error getting user info for {participantId}", ex);
                        
                        // Add participant with ID only
                        _participantsPanel.Children.Add(new TextBlock 
                        { 
                            Text = $"User {participantId}",
                            Margin = new Thickness(15, 2, 0, 2)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _context.Logger.Error("Error updating participants list", ex);
            }
        }
    }
    
    #endregion
    
    #region Event Classes
    
    /// <summary>
    /// Event raised when a session is started
    /// </summary>
    public class SessionStartedEvent
    {
        public string SessionId { get; set; }
        public Guid ChannelId { get; set; }
        public CommunicationMode Mode { get; set; }
        public Guid InitiatorId { get; set; }
    }
    
    /// <summary>
    /// Event raised when a user is invited to a session
    /// </summary>
    public class SessionInvitationEvent
    {
        public string SessionId { get; set; }
        public Guid ChannelId { get; set; }
        public CommunicationMode Mode { get; set; }
        public Guid InitiatorId { get; set; }
    }
    
    /// <summary>
    /// Event raised when a user joins a session
    /// </summary>
    public class UserJoinedEvent
    {
        public string SessionId { get; set; }
        public Guid UserId { get; set; }
    }
    
    /// <summary>
    /// Event raised when a user leaves a session
    /// </summary>
    public class UserLeftEvent
    {
        public string SessionId { get; set; }
        public Guid UserId { get; set; }
    }
    
    #endregion
}