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

        public async Task<UiComponent> StartSessionAsync(Guid channelId, CommunicationMode mode)
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
            
            // Create and return the UI component
            var component = CreateVoiceChatComponent(sessionId);
            
            return await Task.FromResult(component);
        }

        public async Task<UiComponent> JoinSessionAsync(string sessionId)
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
            
            // Create and return the UI component
            var component = CreateVoiceChatComponent(sessionId);
            
            return await Task.FromResult(component);
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

        public UiComponent GetSettingsView()
        {
            // Return a settings UI for configuring the voice chat plugin
            return new UiComponent
            {
                Id = "voice-chat-settings",
                ComponentType = "VoiceChatSettings",
                Configuration = @"{
                    ""microphoneDevice"": ""default"",
                    ""speakerDevice"": ""default"",
                    ""sampleRate"": 48000,
                    ""bitDepth"": 16
                }",
                Properties = new Dictionary<string, string>
                {
                    ["title"] = "Voice Chat Settings"
                }
            };
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

        // Helper method to create a voice chat UI component
        private UiComponent CreateVoiceChatComponent(string sessionId)
        {
            return new UiComponent
            {
                Id = $"voice-chat-{sessionId}",
                ComponentType = "VoiceChatControl",
                Configuration = $@"{{
                    ""sessionId"": ""{sessionId}"",
                    ""status"": ""Connected"",
                    ""isMuted"": false,
                    ""volume"": 80
                }}",
                Properties = new Dictionary<string, string>
                {
                    ["title"] = "Voice Chat",
                    ["statusColor"] = "green"
                }
            };
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
}