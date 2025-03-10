using Drocsid.HenrikDennis2025.PluginContracts.Models;
using Logic.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace UI
{
    public partial class ChatUserControl : UserControl
    {
        private bool _eventSubscribed = false;
        private ChatViewModel _viewModel;

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

        private void ChatViewModel_RequestOpenAddMembersWindow(object sender, System.EventArgs e)
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
        
        private void ViewModel_RequestShowPluginsWindow(object sender, System.EventArgs e)
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
                // Create the plugin UI based on the UiComponent
                var pluginContent = CreatePluginUI(e.UiComponent);
                
                // Set the plugin content to the view model
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _viewModel.ActivePluginContent = pluginContent;
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
            // Here we would normally create the actual UI component based on the UiComponent
            // For this implementation, we'll create a simple placeholder
            ContentControl container = new ContentControl();
            
            // Create the UI based on the component type
            switch (uiComponent.ComponentType)
            {
                case "VoiceChatControl":
                    container.Content = CreateVoiceChatUI(uiComponent);
                    break;
                    
                case "WhiteboardControl":
                    container.Content = CreateWhiteboardUI(uiComponent);
                    break;
                    
                default:
                    // Generic placeholder
                    container.Content = new TextBlock
                    {
                        Text = $"Plugin UI: {uiComponent.Id}",
                        Foreground = System.Windows.Media.Brushes.White
                    };
                    break;
            }
            
            return container;
        }
        
        private UIElement CreateVoiceChatUI(UiComponent uiComponent)
        {
            // Create a simple voice chat UI placeholder
            Border container = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Width = 300,
                Height = 200,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // Create a stack panel for the content
            StackPanel content = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            // Add a title
            TextBlock title = new TextBlock
            {
                Text = "Voice Chat Session",
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            // Add a status indicator
            StackPanel statusPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            Ellipse statusIndicator = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = System.Windows.Media.Brushes.Green,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            
            TextBlock statusText = new TextBlock
            {
                Text = "Connected",
                Foreground = System.Windows.Media.Brushes.LightGray,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            statusPanel.Children.Add(statusIndicator);
            statusPanel.Children.Add(statusText);
            
            // Add a mute button
            Button muteButton = new Button
            {
                Content = "🔇 Mute",
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 5, 0, 5),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(64, 64, 64)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0)
            };
            
            // Add a disconnect button
            Button disconnectButton = new Button
            {
                Content = "❌ Disconnect",
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 5, 0, 5),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0)
            };
            
            disconnectButton.Click += (s, e) => 
            {
                // End the plugin session
                _viewModel.ActivePluginContent = null;
            };
            
            // Add the elements to the panel
            content.Children.Add(title);
            content.Children.Add(statusPanel);
            content.Children.Add(muteButton);
            content.Children.Add(disconnectButton);
            
            // Set the content of the container
            container.Child = content;
            
            return container;
        }
        
        private UIElement CreateWhiteboardUI(UiComponent uiComponent)
        {
            // Create a simple whiteboard UI placeholder
            Border container = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Width = 600,
                Height = 400,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // Create a grid for the content
            Grid content = new Grid();
            
            // Define rows for the grid
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // Add a title
            TextBlock title = new TextBlock
            {
                Text = "Collaborative Whiteboard",
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(title, 0);
            
            // Add a canvas for drawing
            Border canvasBorder = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 5, 0, 5)
            };
            Grid.SetRow(canvasBorder, 1);
            
            Canvas drawingCanvas = new Canvas
            {
                Background = System.Windows.Media.Brushes.Transparent
            };
            canvasBorder.Child = drawingCanvas;
            
            // Add a toolbar
            StackPanel toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };
            Grid.SetRow(toolbar, 2);
            
            // Color picker 
            ComboBox colorPicker = new ComboBox
            {
                Width = 80,
                Margin = new Thickness(5, 0, 5, 0)
            };
            colorPicker.Items.Add("Black");
            colorPicker.Items.Add("Red");
            colorPicker.Items.Add("Blue");
            colorPicker.Items.Add("Green");
            colorPicker.SelectedIndex = 0;
            
            // Thickness slider
            Slider thicknessSlider = new Slider
            {
                Width = 100,
                Minimum = 1,
                Maximum = 10,
                Value = 2,
                Margin = new Thickness(5, 0, 5, 0)
            };
            
            // Clear button
            Button clearButton = new Button
            {
                Content = "Clear",
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(5, 0, 5, 0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(64, 64, 64)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0)
            };
            
            // End session button
            Button endButton = new Button
            {
                Content = "End Session",
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(5, 0, 5, 0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0)
            };
            
            endButton.Click += (s, e) => 
            {
                // End the plugin session
                _viewModel.ActivePluginContent = null;
            };
            
            // Add the elements to the toolbar
            toolbar.Children.Add(colorPicker);
            toolbar.Children.Add(thicknessSlider);
            toolbar.Children.Add(clearButton);
            toolbar.Children.Add(endButton);
            
            // Add all elements to the grid
            content.Children.Add(title);
            content.Children.Add(canvasBorder);
            content.Children.Add(toolbar);
            
            // Set the content of the container
            container.Child = content;
            
            return container;
        }
    }
}