using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;
using Drocsid.HenrikDennis2025.PluginContracts.Models;
using System.Windows.Controls;

namespace VoiceChatPlugin
{
    /// <summary>
    /// A plugin that provides voice chat capabilities for channels
    /// </summary>
    public class VoiceChatPlugin : ICommunicationPlugin
    {
        private IPluginContext _context;
        private readonly Dictionary<string, VoiceSession> _activeSessions = new();

        public string Id => "voice-chat-plugin";
        public string Name => "Voice Chat";
        public string Description => "Enables voice communication between users in a channel";
        public Version Version => new Version(1, 0, 0);
        public string Author => "Henrik Dennis";
        public string InfoUrl => "https://henrikdennis.github.io/drocsid/plugins/voice-chat";
        public PluginState State { get; private set; } = PluginState.Uninitialized;

        public IEnumerable<CommunicationMode> SupportedModes => new[] { CommunicationMode.Audio };

        public async Task InitializeAsync(IPluginContext context)
        {
            _context = context;
            _context.Logger.Info("Voice Chat Plugin initializing...");

            // Set up event subscriptions
            _context.EventManager.Subscribe<object>("channel_opened", OnChannelOpened);
            _context.EventManager.Subscribe<Guid>("channel_closed", OnChannelClosed);

            // Load configuration
            var microphoneDevice = _context.Configuration.GetValue<string>("microphone_device", "default");
            var speakerDevice = _context.Configuration.GetValue<string>("speaker_device", "default");
            var sampleRate = _context.Configuration.GetValue<int>("sample_rate", 48000);
            var bitDepth = _context.Configuration.GetValue<int>("bit_depth", 16);

            _context.Logger.Info($"Audio configuration: {microphoneDevice}/{speakerDevice} @ {sampleRate}Hz/{bitDepth}bit");
            
            State = PluginState.Running;
            _context.Logger.Info("Voice Chat Plugin initialized successfully");
            
            await Task.CompletedTask;
        }

        public async Task ShutdownAsync()
        {
            _context.Logger.Info("Voice Chat Plugin shutting down...");
            
            // End all active sessions
            foreach (var sessionId in _activeSessions.Keys.ToList())
            {
                await EndSessionAsync(sessionId);
            }
            
            // Unsubscribe from events
            _context.EventManager.Unsubscribe<object>("channel_opened", OnChannelOpened);
            _context.EventManager.Unsubscribe<Guid>("channel_closed", OnChannelClosed);
            
            State = PluginState.Disabled;
            _context.Logger.Info("Voice Chat Plugin shutdown complete");
        }

        public async Task<UserControl> StartSessionAsync(Guid channelId, CommunicationMode mode)
        {
            if (mode != CommunicationMode.Audio)
            {
                throw new NotSupportedException($"Communication mode {mode} is not supported by this plugin");
            }

            _context.Logger.Info($"Starting voice session for channel {channelId}");
            
            // Generate a unique session ID
            var sessionId = $"voice-{channelId}-{Guid.NewGuid()}";
            
            // Create a new voice session
            var session = new VoiceSession
            {
                Id = sessionId,
                ChannelId = channelId,
                StartTime = DateTime.UtcNow,
                Participants = new List<Guid>()
            };
            
            // Add the current user as first participant
            session.Participants.Add(_context.UserSession.CurrentUserId);
            
            // Store the session
            _activeSessions[sessionId] = session;
            
            // Create and return the UI control
            var control = new VoiceChatControl(sessionId, _context);
            
            return await Task.FromResult(control);
        }

        public async Task<UserControl> JoinSessionAsync(string sessionId)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                throw new ArgumentException($"Voice session {sessionId} does not exist");
            }
            
            _context.Logger.Info($"Joining voice session {sessionId} for channel {session.ChannelId}");
            
            // Add the current user to participants
            if (!session.Participants.Contains(_context.UserSession.CurrentUserId))
            {
                session.Participants.Add(_context.UserSession.CurrentUserId);
            }
            
            // Create and return the UI control
            var control = new VoiceChatControl(sessionId, _context);
            
            return await Task.FromResult(control);
        }

        public async Task EndSessionAsync(string sessionId)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                throw new ArgumentException($"Voice session {sessionId} does not exist");
            }
            
            _context.Logger.Info($"Ending voice session {sessionId} for channel {session.ChannelId}");
            
            // Remove the session
            _activeSessions.Remove(sessionId);
            
            await Task.CompletedTask;
        }

        public UserControl GetSettingsView()
        {
            // Return a settings UI for configuring the voice chat plugin
            return new VoiceChatSettings(_context);
        }

        private void OnChannelOpened(object channelInfo)
        {
            _context.Logger.Debug("Channel opened event received");
        }

        private void OnChannelClosed(Guid channelId)
        {
            _context.Logger.Debug($"Channel closed event received for channel {channelId}");
            
            // End any voice sessions for this channel
            foreach (var session in _activeSessions.Values.Where(s => s.ChannelId == channelId).ToList())
            {
                _ = EndSessionAsync(session.Id);
            }
        }
    }

    /// <summary>
    /// Represents an active voice session
    /// </summary>
    internal class VoiceSession
    {
        public string Id { get; set; }
        public Guid ChannelId { get; set; }
        public DateTime StartTime { get; set; }
        public List<Guid> Participants { get; set; }
    }

    /// <summary>
    /// Simple UI control for voice chat
    /// </summary>
    internal class VoiceChatControl : UserControl
    {
        private readonly string _sessionId;
        private readonly IPluginContext _context;
        
        public VoiceChatControl(string sessionId, IPluginContext context)
        {
            _sessionId = sessionId;
            _context = context;
            
            // In a real implementation, this would set up the UI for voice chat
            // including microphone level, mute button, participants list, etc.
            
            // For this example, we'll just create a simple placeholder UI
            var grid = new Grid();
            
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            
            var titleLabel = new Label
            {
                Content = "Voice Chat",
                FontWeight = System.Windows.FontWeights.Bold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };
            
            var statusLabel = new Label
            {
                Content = "Connected",
                Foreground = System.Windows.Media.Brushes.Green
            };
            
            var muteButton = new Button
            {
                Content = "Mute",
                Margin = new System.Windows.Thickness(5),
                Padding = new System.Windows.Thickness(10, 5, 10, 5)
            };
            
            stackPanel.Children.Add(titleLabel);
            stackPanel.Children.Add(statusLabel);
            stackPanel.Children.Add(muteButton);
            
            grid.Children.Add(stackPanel);
            
            Content = grid;
        }
    }

    /// <summary>
    /// Settings UI for voice chat plugin
    /// </summary>
    internal class VoiceChatSettings : UserControl
    {
        private readonly IPluginContext _context;
        
        public VoiceChatSettings(IPluginContext context)
        {
            _context = context;
            
            // In a real implementation, this would set up the UI for voice chat settings
            // including microphone selection, speaker selection, etc.
            
            // For this example, we'll just create a simple placeholder UI
            var grid = new Grid();
            
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            
            var titleLabel = new Label
            {
                Content = "Voice Chat Settings",
                FontWeight = System.Windows.FontWeights.Bold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };
            
            var microphoneLabel = new Label
            {
                Content = "Microphone Device:"
            };
            
            var microphoneComboBox = new ComboBox
            {
                Margin = new System.Windows.Thickness(5),
                MinWidth = 200
            };
            
            var speakerLabel = new Label
            {
                Content = "Speaker Device:"
            };
            
            var speakerComboBox = new ComboBox
            {
                Margin = new System.Windows.Thickness(5),
                MinWidth = 200
            };
            
            var saveButton = new Button
            {
                Content = "Save Settings",
                Margin = new System.Windows.Thickness(5),
                Padding = new System.Windows.Thickness(10, 5, 10, 5)
            };
            
            stackPanel.Children.Add(titleLabel);
            stackPanel.Children.Add(microphoneLabel);
            stackPanel.Children.Add(microphoneComboBox);
            stackPanel.Children.Add(speakerLabel);
            stackPanel.Children.Add(speakerComboBox);
            stackPanel.Children.Add(saveButton);
            
            grid.Children.Add(stackPanel);
            
            Content = grid;
        }
    }
}