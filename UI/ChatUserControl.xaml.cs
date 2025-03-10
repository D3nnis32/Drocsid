using Drocsid.HenrikDennis2025.PluginContracts.Models;
using Logic.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace UI
{
    public partial class ChatUserControl : UserControl
    {
        private bool _eventSubscribed = false;
        private ChatViewModel _viewModel;

        // Variables for drawing functionality
        private bool _isDrawing = false;
        private Point _lastPoint;
        private Canvas _drawingCanvas;
        private Color _currentColor = Colors.Black;
        private double _currentThickness = 2;
        private bool _isMuted = false;

        public ChatUserControl()
        {
            InitializeComponent();
            DataContextChanged += ChatUserControl_DataContextChanged;
        }

        private void ChatUserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ChatViewModel oldViewModel)
            {
                oldViewModel.RequestOpenAddMembersWindow -= ChatViewModel_RequestOpenAddMembersWindow;
                oldViewModel.RequestShowPluginsWindow -= ViewModel_RequestShowPluginsWindow;

                // Unsubscribe from plugin events
                oldViewModel.PluginSessionStarted -= OnPluginSessionStarted;

                _eventSubscribed = false;
            }

            if (e.NewValue is ChatViewModel newViewModel && !_eventSubscribed)
            {
                newViewModel.RequestOpenAddMembersWindow += ChatViewModel_RequestOpenAddMembersWindow;
                newViewModel.RequestShowPluginsWindow += ViewModel_RequestShowPluginsWindow;

                // Subscribe to plugin events
                newViewModel.PluginSessionStarted += OnPluginSessionStarted;

                _viewModel = newViewModel;
                _eventSubscribed = true;
            }
        }

        private void ChatViewModel_RequestOpenAddMembersWindow(object sender, EventArgs e)
        {
            var addMembersWindow = new AddMembersWindow();
            // Retrieve the ChatViewModel from the sender and pass the channel ID to the AddMembersViewModel.
            if (sender is ChatViewModel chatVM)
            {
                addMembersWindow.DataContext = new AddMembersViewModel(chatVM.Channel.Id);
            }
            else
            {
                // Fallback in case of an issue.
                addMembersWindow.DataContext = new AddMembersViewModel(Guid.Empty);
            }

            addMembersWindow.Owner = Window.GetWindow(this);
            addMembersWindow.ShowDialog();
        }

        private void ViewModel_RequestShowPluginsWindow(object sender, EventArgs e)
        {
            var pluginManager = new PluginManagerWindow();
            var viewModel = new PluginManagerViewModel(pluginManager);
            pluginManager.DataContext = viewModel;
            pluginManager.Owner = Window.GetWindow(this);
            pluginManager.ShowDialog();
        }

        private void OnPluginSessionStarted(object sender, PluginSessionEventArgs e)
        {
            try
            {
                Console.WriteLine(
                    $"DEBUG: Plugin session started event received. Type: {e.PluginType}, ID: {e.SessionId}");

                // Create the plugin UI based on the UiComponent
                var pluginContent = CreatePluginUI(e.UiComponent);

                Console.WriteLine($"DEBUG: Plugin UI created: {pluginContent != null}");

                // Set the plugin content to the view model
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _viewModel.ActivePluginContent = pluginContent;
                    Console.WriteLine(
                        $"DEBUG: Plugin content set to ViewModel. IsPluginActive: {_viewModel.IsPluginActive}");
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating plugin UI: {ex.Message}");
                _viewModel.ErrorMessage = "Failed to start plugin session.";
            }
        }

        private ContentControl CreatePluginUI(UiComponent uiComponent)
        {
            // Create the content control container
            ContentControl container = new ContentControl();

            // Create the UI based on the component type
            switch (uiComponent.ComponentType.ToLower())
            {
                case "communicationview":
                case "voicechatcontrol":
                    container.Content = CreateVoiceChatUI(uiComponent);
                    break;

                case "collaborationview":
                case "whiteboardcontrol":
                    container.Content = CreateWhiteboardUI(uiComponent);
                    break;

                default:
                    // Use component info in the fallback case
                    Border fallbackUI = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(10),
                        Margin = new Thickness(20),
                        BorderBrush = new SolidColorBrush(Colors.Gray),
                        BorderThickness = new Thickness(1)
                    };

                    StackPanel content = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Margin = new Thickness(10)
                    };

                    TextBlock title = new TextBlock
                    {
                        Text = $"Plugin Component: {uiComponent.ComponentType}",
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        FontSize = 16,
                        Margin = new Thickness(0, 0, 0, 10),
                        TextAlignment = TextAlignment.Center
                    };

                    TextBlock infoText = new TextBlock
                    {
                        Text = $"ID: {uiComponent.Id}\nProperties: {uiComponent.Properties.Count} properties",
                        Foreground = Brushes.LightGray,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 5, 0, 10),
                    };

                    Button closeButton = new Button
                    {
                        Content = "Close Session",
                        Padding = new Thickness(10, 5, 10, 5),
                        Margin = new Thickness(0, 10, 0, 0),
                        Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(0),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                    closeButton.Click += (s, e) => { _viewModel.ActivePluginContent = null; };

                    content.Children.Add(title);
                    content.Children.Add(infoText);
                    content.Children.Add(closeButton);
                    fallbackUI.Child = content;
                    container.Content = fallbackUI;
                    break;
            }

            return container;
        }

        private UIElement CreateVoiceChatUI(UiComponent uiComponent)
        {
            // Create a more functional voice chat UI
            Border container = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(15),
                Width = 350,
                Height = 300,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20)
            };

            // Create a grid layout
            Grid content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status
            content.RowDefinitions.Add(new RowDefinition
                { Height = new GridLength(1, GridUnitType.Star) }); // Participants
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Controls

            // Header with title and info
            StackPanel header = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 15)
            };

            TextBlock title = new TextBlock
            {
                Text = "Voice Chat Session",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            TextBlock sessionInfo = new TextBlock
            {
                Text =
                    $"Session ID: {uiComponent.Configuration.Substring(0, Math.Min(15, uiComponent.Configuration.Length))}...",
                Foreground = Brushes.LightGray,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };

            header.Children.Add(title);
            header.Children.Add(sessionInfo);
            Grid.SetRow(header, 0);

            // Status indicator
            Border statusPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 35)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 0, 15)
            };

            StackPanel statusContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            Ellipse statusIndicator = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.LimeGreen,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock statusText = new TextBlock
            {
                Text = "Connected",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            };

            statusContent.Children.Add(statusIndicator);
            statusContent.Children.Add(statusText);
            statusPanel.Child = statusContent;
            Grid.SetRow(statusPanel, 1);

            // Participants list
            Border participantsPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 35)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 15)
            };

            StackPanel participantsList = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            // Add current user (self)
            Border selfUserBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 0, 5)
            };

            Grid selfUserGrid = new Grid();
            selfUserGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            selfUserGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel selfUserInfo = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            Ellipse selfUserAvatar = new Ellipse
            {
                Width = 24,
                Height = 24,
                Fill = Brushes.RoyalBlue,
                Margin = new Thickness(0, 0, 8, 0)
            };

            TextBlock selfUsername = new TextBlock
            {
                Text = "You",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            };

            selfUserInfo.Children.Add(selfUserAvatar);
            selfUserInfo.Children.Add(selfUsername);
            Grid.SetColumn(selfUserInfo, 0);

            // Sound wave visualization
            Canvas soundWave = new Canvas
            {
                Width = 60,
                Height = 20
            };

            // Draw sound waves
            for (int i = 0; i < 5; i++)
            {
                Line line = new Line
                {
                    X1 = i * 12 + 5,
                    Y1 = 10,
                    X2 = i * 12 + 5,
                    Y2 = _isMuted ? 10 : (i % 2 == 0 ? 4 : 16),
                    Stroke = _isMuted ? Brushes.Red : Brushes.LimeGreen,
                    StrokeThickness = 2
                };
                soundWave.Children.Add(line);
            }

            Grid.SetColumn(soundWave, 1);

            selfUserGrid.Children.Add(selfUserInfo);
            selfUserGrid.Children.Add(soundWave);
            selfUserBorder.Child = selfUserGrid;

            // Add another participant
            Border otherUserBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 5, 10, 5)
            };

            Grid otherUserGrid = new Grid();
            otherUserGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            otherUserGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel otherUserInfo = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            Ellipse otherUserAvatar = new Ellipse
            {
                Width = 24,
                Height = 24,
                Fill = Brushes.Orange,
                Margin = new Thickness(0, 0, 8, 0)
            };

            TextBlock otherUsername = new TextBlock
            {
                Text = "User1",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            otherUserInfo.Children.Add(otherUserAvatar);
            otherUserInfo.Children.Add(otherUsername);
            Grid.SetColumn(otherUserInfo, 0);

            // Sound wave visualization for other user
            Canvas otherSoundWave = new Canvas
            {
                Width = 60,
                Height = 20
            };

            // Draw sound waves for other user (currently speaking)
            Random random = new Random();
            for (int i = 0; i < 5; i++)
            {
                double height = random.Next(4, 16);
                Line line = new Line
                {
                    X1 = i * 12 + 5,
                    Y1 = 10,
                    X2 = i * 12 + 5,
                    Y2 = 10 - height / 2,
                    Stroke = Brushes.LimeGreen,
                    StrokeThickness = 2
                };
                Line line2 = new Line
                {
                    X1 = i * 12 + 5,
                    Y1 = 10,
                    X2 = i * 12 + 5,
                    Y2 = 10 + height / 2,
                    Stroke = Brushes.LimeGreen,
                    StrokeThickness = 2
                };
                otherSoundWave.Children.Add(line);
                otherSoundWave.Children.Add(line2);
            }

            Grid.SetColumn(otherSoundWave, 1);

            otherUserGrid.Children.Add(otherUserInfo);
            otherUserGrid.Children.Add(otherSoundWave);
            otherUserBorder.Child = otherUserGrid;

            participantsList.Children.Add(selfUserBorder);
            participantsList.Children.Add(otherUserBorder);
            participantsPanel.Child = participantsList;
            Grid.SetRow(participantsPanel, 2);

            // Control buttons
            StackPanel controls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };

            Button muteButton = new Button
            {
                Content = _isMuted ? "🔇 Unmute" : "🎤 Mute",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(5, 0, 5, 0),
                Background = _isMuted
                    ? new SolidColorBrush(Color.FromRgb(220, 53, 69))
                    : new SolidColorBrush(Color.FromRgb(52, 58, 64)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            muteButton.Click += (s, e) =>
            {
                _isMuted = !_isMuted;
                muteButton.Content = _isMuted ? "🔇 Unmute" : "🎤 Mute";
                muteButton.Background = _isMuted
                    ? new SolidColorBrush(Color.FromRgb(220, 53, 69))
                    : new SolidColorBrush(Color.FromRgb(52, 58, 64));

                // Update sound wave visualization
                int index = 0;
                foreach (UIElement element in soundWave.Children)
                {
                    if (element is Line line)
                    {
                        line.Y2 = _isMuted ? 10 : (index % 2 == 0 ? 4 : 16);
                        line.Stroke = _isMuted ? Brushes.Red : Brushes.LimeGreen;
                        index++;
                    }
                }
            };

            Button disconnectButton = new Button
            {
                Content = "Disconnect",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(5, 0, 5, 0),
                Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            disconnectButton.Click += (s, e) => { _viewModel.ActivePluginContent = null; };

            controls.Children.Add(muteButton);
            controls.Children.Add(disconnectButton);
            Grid.SetRow(controls, 3);

            // Add all elements to the grid
            content.Children.Add(header);
            content.Children.Add(statusPanel);
            content.Children.Add(participantsPanel);
            content.Children.Add(controls);

            // Set the content of the container
            container.Child = content;

            return container;
        }

        private UIElement CreateWhiteboardUI(UiComponent uiComponent)
        {
            // Create a more functional whiteboard UI
            Border container = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(15),
                Width = 700,
                Height = 500,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20)
            };

            // Create a grid layout
            Grid content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Canvas
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Toolbar

            // Header with title and info
            StackPanel header = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 10)
            };

            TextBlock title = new TextBlock
            {
                Text = "Collaborative Whiteboard",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            TextBlock sessionInfo = new TextBlock
            {
                Text =
                    $"Session ID: {uiComponent.Configuration.Substring(0, Math.Min(15, uiComponent.Configuration.Length))}...",
                Foreground = Brushes.LightGray,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };

            header.Children.Add(title);
            header.Children.Add(sessionInfo);
            Grid.SetRow(header, 0);

            // Canvas for drawing
            Border canvasBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 5, 0, 10)
            };

            _drawingCanvas = new Canvas
            {
                Background = Brushes.Transparent,
                ClipToBounds = true
            };

            // Add mouse event handlers for drawing
            _drawingCanvas.MouseDown += (s, e) =>
            {
                _isDrawing = true;
                _lastPoint = e.GetPosition(_drawingCanvas);
            };

            _drawingCanvas.MouseMove += (s, e) =>
            {
                if (_isDrawing)
                {
                    Point currentPoint = e.GetPosition(_drawingCanvas);

                    // Create a line
                    Line line = new Line
                    {
                        X1 = _lastPoint.X,
                        Y1 = _lastPoint.Y,
                        X2 = currentPoint.X,
                        Y2 = currentPoint.Y,
                        Stroke = new SolidColorBrush(_currentColor),
                        StrokeThickness = _currentThickness,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    };

                    _drawingCanvas.Children.Add(line);
                    _lastPoint = currentPoint;
                }
            };

            _drawingCanvas.MouseUp += (s, e) => { _isDrawing = false; };

            _drawingCanvas.MouseLeave += (s, e) => { _isDrawing = false; };

            canvasBorder.Child = _drawingCanvas;
            Grid.SetRow(canvasBorder, 1);

            // Toolbar
            Border toolbarBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 35)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
            };

            // Create a grid for the toolbar
            Grid toolbar = new Grid();
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Colors
            toolbar.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) }); // Thickness slider
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Buttons

            // Color picker section
            StackPanel colorPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 15, 0)
            };

            TextBlock colorLabel = new TextBlock
            {
                Text = "Colors:",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            // Color buttons
            Color[] colors =
                { Colors.Black, Colors.Red, Colors.Blue, Colors.Green, Colors.Yellow, Colors.Orange, Colors.Purple };

            foreach (Color color in colors)
            {
                Border colorButton = new Border
                {
                    Width = 24,
                    Height = 24,
                    Background = new SolidColorBrush(color),
                    BorderBrush = color == _currentColor ? Brushes.White : Brushes.Gray,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(3, 0, 3, 0),
                    Cursor = Cursors.Hand
                };

                colorButton.MouseDown += (s, e) =>
                {
                    _currentColor = color;

                    // Update border highlights
                    foreach (UIElement element in colorPanel.Children)
                    {
                        if (element is Border border)
                        {
                            // Skip if it's not a proper color button
                            SolidColorBrush bgBrush = border.Background as SolidColorBrush;
                            if (bgBrush != null)
                            {
                                border.BorderBrush = bgBrush.Color == _currentColor
                                    ? Brushes.White
                                    : Brushes.Gray;
                            }
                        }
                    }
                };

                colorPanel.Children.Add(colorButton);
            }

            Grid.SetColumn(colorPanel, 0);

            // Thickness slider section
            StackPanel thicknessPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 0, 10, 0)
            };

            TextBlock thicknessLabel = new TextBlock
            {
                Text = "Thickness:",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            Slider thicknessSlider = new Slider
            {
                Minimum = 1,
                Maximum = 20,
                Value = _currentThickness,
                Width = 150,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock thicknessValue = new TextBlock
            {
                Text = $"{_currentThickness:F1}",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                MinWidth = 30
            };

            thicknessSlider.ValueChanged += (s, e) =>
            {
                _currentThickness = thicknessSlider.Value;
                thicknessValue.Text = $"{_currentThickness:F1}";
            };

            thicknessPanel.Children.Add(thicknessLabel);
            thicknessPanel.Children.Add(thicknessSlider);
            thicknessPanel.Children.Add(thicknessValue);

            Grid.SetColumn(thicknessPanel, 1);

            // Control buttons section
            StackPanel buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            Button clearButton = new Button
            {
                Content = "Clear",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(5, 0, 5, 0),
                Background = new SolidColorBrush(Color.FromRgb(52, 58, 64)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            clearButton.Click += (s, e) => { _drawingCanvas.Children.Clear(); };

            Button endButton = new Button
            {
                Content = "End Session",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(5, 0, 5, 0),
                Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            endButton.Click += (s, e) => { _viewModel.ActivePluginContent = null; };

            buttonsPanel.Children.Add(clearButton);
            buttonsPanel.Children.Add(endButton);

            Grid.SetColumn(buttonsPanel, 2);

            // Add elements to toolbar
            toolbar.Children.Add(colorPanel);
            toolbar.Children.Add(thicknessPanel);
            toolbar.Children.Add(buttonsPanel);

            toolbarBorder.Child = toolbar;
            Grid.SetRow(toolbarBorder, 2);

            // Add all elements to the grid
            content.Children.Add(header);
            content.Children.Add(canvasBorder);
            content.Children.Add(toolbarBorder);

            // Set the content of the container
            container.Child = content;

            return container;
        }
    }
}