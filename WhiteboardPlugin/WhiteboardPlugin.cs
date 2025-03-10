using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;
using Drocsid.HenrikDennis2025.PluginContracts.Models;

namespace WhiteboardPlugin
{
    /// <summary>
    /// A plugin that provides a shared whiteboard for collaboration
    /// </summary>
    public class WhiteboardPlugin : ICollaborationPlugin
    {
        private IPluginContext _context;
        private readonly Dictionary<string, WhiteboardSession> _activeSessions = new();

        public string Id => "whiteboard-plugin";
        public string Name => "Shared Whiteboard";
        public string Description => "Enables collaborative drawing and diagramming in channels";
        public Version Version => new Version(1, 0, 0);
        public string Author => "Henrik Dennis";
        public string InfoUrl => "https://henrikdennis.github.io/drocsid/plugins/whiteboard";
        public PluginState State { get; private set; } = PluginState.Uninitialized;

        public async Task InitializeAsync(IPluginContext context)
        {
            _context = context;
            _context.Logger.Info("Whiteboard Plugin initializing...");

            // Set up event subscriptions
            _context.EventManager.Subscribe<object>("channel_opened", OnChannelOpened);
            _context.EventManager.Subscribe<Guid>("channel_closed", OnChannelClosed);
            _context.EventManager.Subscribe<WhiteboardEvent>("whiteboard_event", OnWhiteboardEvent);

            // Load configuration
            var defaultStrokeColor = _context.Configuration.GetValue<string>("default_stroke_color", "#000000");
            var defaultStrokeWidth = _context.Configuration.GetValue<double>("default_stroke_width", 2.0);
            var maxCanvasWidth = _context.Configuration.GetValue<double>("max_canvas_width", 1200.0);
            var maxCanvasHeight = _context.Configuration.GetValue<double>("max_canvas_height", 800.0);

            _context.Logger.Info($"Whiteboard configuration: Color={defaultStrokeColor}, Width={defaultStrokeWidth}, Max size: {maxCanvasWidth}x{maxCanvasHeight}");
            
            State = PluginState.Running;
            _context.Logger.Info("Whiteboard Plugin initialized successfully");
            
            await Task.CompletedTask;
        }

        public async Task ShutdownAsync()
        {
            _context.Logger.Info("Whiteboard Plugin shutting down...");
            
            // End all active sessions
            foreach (var sessionId in _activeSessions.Keys.ToList())
            {
                await EndCollaborationAsync(sessionId);
            }
            
            // Unsubscribe from events
            _context.EventManager.Unsubscribe<object>("channel_opened", OnChannelOpened);
            _context.EventManager.Unsubscribe<Guid>("channel_closed", OnChannelClosed);
            _context.EventManager.Unsubscribe<WhiteboardEvent>("whiteboard_event", OnWhiteboardEvent);
            
            State = PluginState.Disabled;
            _context.Logger.Info("Whiteboard Plugin shutdown complete");
        }

        public async Task<UiComponent> StartCollaborationAsync(Guid channelId)
        {
            _context.Logger.Info($"Starting whiteboard session for channel {channelId}");
            
            // Generate a unique session ID
            var sessionId = $"whiteboard-{channelId}-{Guid.NewGuid()}";
            
            // Create a new whiteboard session
            var session = new WhiteboardSession
            {
                Id = sessionId,
                ChannelId = channelId,
                StartTime = DateTime.UtcNow,
                Participants = new List<Guid>(),
                Strokes = new List<WhiteboardStroke>()
            };
            
            // Add the current user as first participant
            session.Participants.Add(_context.UserSession.CurrentUserId);
            
            // Store the session
            _activeSessions[sessionId] = session;
            
            // Create and return the UI component
            var component = CreateWhiteboardComponent(sessionId, session);
            
            return await Task.FromResult(component);
        }

        public async Task<UiComponent> JoinCollaborationAsync(string sessionId)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                throw new ArgumentException($"Whiteboard session {sessionId} does not exist");
            }
            
            _context.Logger.Info($"Joining whiteboard session {sessionId} for channel {session.ChannelId}");
            
            // Add the current user to participants
            if (!session.Participants.Contains(_context.UserSession.CurrentUserId))
            {
                session.Participants.Add(_context.UserSession.CurrentUserId);
            }
            
            // Create and return the UI component
            var component = CreateWhiteboardComponent(sessionId, session);
            
            return await Task.FromResult(component);
        }

        public async Task EndCollaborationAsync(string sessionId)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                throw new ArgumentException($"Whiteboard session {sessionId} does not exist");
            }
            
            _context.Logger.Info($"Ending whiteboard session {sessionId} for channel {session.ChannelId}");
            
            // Notify participants that the session is ending
            var endEvent = new WhiteboardEvent
            {
                SessionId = sessionId,
                EventType = WhiteboardEventType.SessionEnded,
                UserId = _context.UserSession.CurrentUserId
            };
            
            _context.EventManager.Publish("whiteboard_event", endEvent);
            
            // Remove the session
            _activeSessions.Remove(sessionId);
            
            await Task.CompletedTask;
        }

        public UiComponent GetSettingsView()
        {
            // Return a settings UI for configuring the whiteboard plugin
            return new UiComponent
            {
                Id = "whiteboard-settings",
                ComponentType = "WhiteboardSettings",
                Configuration = @"{
                    ""defaultStrokeColor"": ""#000000"",
                    ""defaultStrokeWidth"": 2.0,
                    ""maxCanvasWidth"": 1200.0,
                    ""maxCanvasHeight"": 800.0
                }",
                Properties = new Dictionary<string, string>
                {
                    ["title"] = "Whiteboard Settings"
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
            
            // End any whiteboard sessions for this channel
            foreach (var session in _activeSessions.Values.Where(s => s.ChannelId == channelId).ToList())
            {
                _ = EndCollaborationAsync(session.Id);
            }
        }

        private void OnWhiteboardEvent(WhiteboardEvent evt)
        {
            if (!_activeSessions.TryGetValue(evt.SessionId, out var session))
            {
                _context.Logger.Warning($"Received whiteboard event for unknown session: {evt.SessionId}");
                return;
            }
            
            _context.Logger.Debug($"Whiteboard event received: {evt.EventType} from user {evt.UserId}");
            
            // Process the event based on its type
            switch (evt.EventType)
            {
                case WhiteboardEventType.StrokeAdded:
                    if (evt.Stroke != null)
                    {
                        session.Strokes.Add(evt.Stroke);
                    }
                    break;
                
                case WhiteboardEventType.StrokeUpdated:
                    if (evt.Stroke != null && evt.StrokeIndex >= 0 && evt.StrokeIndex < session.Strokes.Count)
                    {
                        session.Strokes[evt.StrokeIndex] = evt.Stroke;
                    }
                    break;
                
                case WhiteboardEventType.StrokeRemoved:
                    if (evt.StrokeIndex >= 0 && evt.StrokeIndex < session.Strokes.Count)
                    {
                        session.Strokes.RemoveAt(evt.StrokeIndex);
                    }
                    break;
                
                case WhiteboardEventType.CanvasCleared:
                    session.Strokes.Clear();
                    break;
                
                case WhiteboardEventType.ParticipantJoined:
                    if (!session.Participants.Contains(evt.UserId))
                    {
                        session.Participants.Add(evt.UserId);
                    }
                    break;
                
                case WhiteboardEventType.ParticipantLeft:
                    session.Participants.Remove(evt.UserId);
                    break;
                
                case WhiteboardEventType.SessionEnded:
                    _activeSessions.Remove(evt.SessionId);
                    break;
            }
        }

        // Helper method to create a whiteboard UI component
        private UiComponent CreateWhiteboardComponent(string sessionId, WhiteboardSession session)
        {
            // Convert the strokes to a JSON string for the configuration
            var strokesJson = JsonSerializer.Serialize(session.Strokes);
            
            return new UiComponent
            {
                Id = $"whiteboard-{sessionId}",
                ComponentType = "WhiteboardControl",
                Configuration = $@"{{
                    ""sessionId"": ""{sessionId}"",
                    ""channelId"": ""{session.ChannelId}"",
                    ""currentColor"": ""#000000"",
                    ""currentThickness"": 2.0,
                    ""strokes"": {strokesJson},
                    ""participants"": {JsonSerializer.Serialize(session.Participants)}
                }}",
                Properties = new Dictionary<string, string>
                {
                    ["title"] = "Shared Whiteboard",
                    ["width"] = "800",
                    ["height"] = "600"
                }
            };
        }
    }

    /// <summary>
    /// Represents an active whiteboard session
    /// </summary>
    internal class WhiteboardSession
    {
        public string Id { get; set; }
        public Guid ChannelId { get; set; }
        public DateTime StartTime { get; set; }
        public List<Guid> Participants { get; set; }
        public List<WhiteboardStroke> Strokes { get; set; }
    }

    /// <summary>
    /// Represents a stroke on the whiteboard
    /// </summary>
    internal class WhiteboardStroke
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public List<Point> Points { get; set; }
        public string Color { get; set; }
        public double Thickness { get; set; }
        public StrokeType Type { get; set; }
    }

    /// <summary>
    /// Represents a 2D point
    /// </summary>
    internal class Point
    {
        public double X { get; set; }
        public double Y { get; set; }
        
        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Type of stroke
    /// </summary>
    internal enum StrokeType
    {
        Freehand,
        Line,
        Rectangle,
        Ellipse,
        Text
    }

    /// <summary>
    /// Type of whiteboard event
    /// </summary>
    internal enum WhiteboardEventType
    {
        StrokeAdded,
        StrokeUpdated,
        StrokeRemoved,
        CanvasCleared,
        ParticipantJoined,
        ParticipantLeft,
        SessionEnded
    }

    /// <summary>
    /// Event data for whiteboard events
    /// </summary>
    internal class WhiteboardEvent
    {
        public string SessionId { get; set; }
        public WhiteboardEventType EventType { get; set; }
        public Guid UserId { get; set; }
        public WhiteboardStroke Stroke { get; set; }
        public int StrokeIndex { get; set; }
    }
}