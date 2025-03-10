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

        public async Task<UserControl> StartCollaborationAsync(Guid channelId)
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
            
            // Create and return the UI control
            var control = new WhiteboardControl(sessionId, _context);
            
            return await Task.FromResult(control);
        }

        public async Task<UserControl> JoinCollaborationAsync(string sessionId)
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
            
            // Create and return the UI control
            var control = new WhiteboardControl(sessionId, _context);
            
            return await Task.FromResult(control);
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

        public UserControl GetSettingsView()
        {
            // Return a settings UI for configuring the whiteboard plugin
            return new WhiteboardSettings(_context);
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

    /// <summary>
    /// Simple UI control for whiteboard
    /// </summary>
    internal class WhiteboardControl : UserControl
    {
        private readonly string _sessionId;
        private readonly IPluginContext _context;
        private Canvas _canvas;
        private Point? _lastPoint;
        private Polyline _currentStroke;
        private string _currentColor = "#000000";
        private double _currentThickness = 2.0;
        
        public WhiteboardControl(string sessionId, IPluginContext context)
        {
            _sessionId = sessionId;
            _context = context;
            
            // Create the UI for the whiteboard
            var grid = new Grid();
            
            // Main toolbar
            var toolBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Height = 40,
                Background = Brushes.LightGray
            };
            
            var clearButton = new Button
            {
                Content = "Clear",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5)
            };
            clearButton.Click += (s, e) => ClearCanvas();
            
            var colorPicker = new ComboBox
            {
                Margin = new Thickness(5),
                Width = 100
            };
            colorPicker.Items.Add("Black");
            colorPicker.Items.Add("Red");
            colorPicker.Items.Add("Blue");
            colorPicker.Items.Add("Green");
            colorPicker.SelectedIndex = 0;
            colorPicker.SelectionChanged += (s, e) => 
            {
                switch (colorPicker.SelectedIndex)
                {
                    case 0: _currentColor = "#000000"; break;
                    case 1: _currentColor = "#FF0000"; break;
                    case 2: _currentColor = "#0000FF"; break;
                    case 3: _currentColor = "#00FF00"; break;
                }
            };
            
            var thicknessSlider = new Slider
            {
                Minimum = 1,
                Maximum = 10,
                Value = 2,
                Width = 100,
                Margin = new Thickness(5)
            };
            thicknessSlider.ValueChanged += (s, e) => 
            {
                _currentThickness = thicknessSlider.Value;
            };
            
            toolBar.Children.Add(clearButton);
            toolBar.Children.Add(new Label { Content = "Color:" });
            toolBar.Children.Add(colorPicker);
            toolBar.Children.Add(new Label { Content = "Thickness:" });
            toolBar.Children.Add(thicknessSlider);
            
            // Drawing canvas
            _canvas = new Canvas
            {
                Background = Brushes.White,
                ClipToBounds = true
            };
            
            _canvas.MouseDown += Canvas_MouseDown;
            _canvas.MouseMove += Canvas_MouseMove;
            _canvas.MouseUp += Canvas_MouseUp;
            
            // Set up the grid
            Grid.SetRow(toolBar, 0);
            Grid.SetRow(_canvas, 1);
            
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            
            grid.Children.Add(toolBar);
            grid.Children.Add(_canvas);
            
            Content = grid;
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _lastPoint = e.GetPosition(_canvas);
                
                // Start a new stroke
                _currentStroke = new Polyline
                {
                    Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(_currentColor),
                    StrokeThickness = _currentThickness,
                    Points = new PointCollection { _lastPoint.Value }
                };
                
                _canvas.Children.Add(_currentStroke);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _lastPoint.HasValue && _currentStroke != null)
            {
                var currentPoint = e.GetPosition(_canvas);
                _currentStroke.Points.Add(currentPoint);
                _lastPoint = currentPoint;
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_currentStroke != null)
            {
                // Finalize the stroke and send it to other participants
                var points = _currentStroke.Points.Select(p => new Point(p.X, p.Y)).ToList();
                
                var stroke = new WhiteboardStroke
                {
                    Id = Guid.NewGuid(),
                    UserId = _context.UserSession.CurrentUserId,
                    Points = points,
                    Color = _currentColor,
                    Thickness = _currentThickness,
                    Type = StrokeType.Freehand
                };
                
                var strokeEvent = new WhiteboardEvent
                {
                    SessionId = _sessionId,
                    EventType = WhiteboardEventType.StrokeAdded,
                    UserId = _context.UserSession.CurrentUserId,
                    Stroke = stroke
                };
                
                _context.EventManager.Publish("whiteboard_event", strokeEvent);
                
                _currentStroke = null;
                _lastPoint = null;
            }
        }

        private void ClearCanvas()
        {
            _canvas.Children.Clear();
            
            var clearEvent = new WhiteboardEvent
            {
                SessionId = _sessionId,
                EventType = WhiteboardEventType.CanvasCleared,
                UserId = _context.UserSession.CurrentUserId
            };
            
            _context.EventManager.Publish("whiteboard_event", clearEvent);
        }
    }

    /// <summary>
    /// Settings UI for whiteboard plugin
    /// </summary>
    internal class WhiteboardSettings : UserControl
    {
        private readonly IPluginContext _context;
        
        public WhiteboardSettings(IPluginContext context)
        {
            _context = context;
            
            // Create the UI for whiteboard settings
            var grid = new Grid();
            
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            
            var titleLabel = new Label
            {
                Content = "Whiteboard Settings",
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            var defaultColorLabel = new Label
            {
                Content = "Default Stroke Color:"
            };
            
            var defaultColorComboBox = new ComboBox
            {
                Margin = new Thickness(5),
                MinWidth = 200
            };
            defaultColorComboBox.Items.Add("Black");
            defaultColorComboBox.Items.Add("Red");
            defaultColorComboBox.Items.Add("Blue");
            defaultColorComboBox.Items.Add("Green");
            defaultColorComboBox.SelectedIndex = 0;
            
            var defaultWidthLabel = new Label
            {
                Content = "Default Stroke Width:"
            };
            
            var defaultWidthSlider = new Slider
            {
                Minimum = 1,
                Maximum = 10,
                Value = 2,
                Margin = new Thickness(5),
                MinWidth = 200
            };
            
            var saveButton = new Button
            {
                Content = "Save Settings",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5)
            };
            
            saveButton.Click += async (s, e) =>
            {
                string colorValue = "#000000";
                switch (defaultColorComboBox.SelectedIndex)
                {
                    case 0: colorValue = "#000000"; break;
                    case 1: colorValue = "#FF0000"; break;
                    case 2: colorValue = "#0000FF"; break;
                    case 3: colorValue = "#00FF00"; break;
                }
                
                _context.Configuration.SetValue("default_stroke_color", colorValue);
                _context.Configuration.SetValue("default_stroke_width", defaultWidthSlider.Value);
                
                await _context.Configuration.SaveAsync();
                
                _context.Logger.Info("Whiteboard settings saved");
                _context.UIService.ShowNotification("Settings Saved", "Whiteboard settings have been saved.", NotificationType.Success);
            };
            
            stackPanel.Children.Add(titleLabel);
            stackPanel.Children.Add(defaultColorLabel);
            stackPanel.Children.Add(defaultColorComboBox);
            stackPanel.Children.Add(defaultWidthLabel);
            stackPanel.Children.Add(defaultWidthSlider);
            stackPanel.Children.Add(saveButton);
            
            grid.Children.Add(stackPanel);
            
            Content = grid;
        }
    }
}