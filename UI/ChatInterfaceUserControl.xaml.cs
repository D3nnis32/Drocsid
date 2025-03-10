using Logic.UI.ViewModels;
using System.Windows.Controls;
using System.Windows;

namespace UI
{
    public partial class ChatInterfaceUserControl : UserControl
    {
        private bool _eventSubscribed = false;

        public ChatInterfaceUserControl()
        {
            InitializeComponent();

            // Only use DataContextChanged for subscription management
            // Remove the Loaded event handler
            DataContextChanged += UserControl_DataContextChanged;
        }

        private void ViewModel_RequestOpenNewChannelWindow(object sender, ChatInterfaceUserControlViewModel.OpenNewChannelWindowEventArgs e)
        {
            // Create and show the window
            var window = new AddChannelWindow();
            var viewModel = new AddChannelWindowViewModel();

            // Set up window events
            window.Closed += (s, args) => e.OnWindowClosed?.Invoke();

            // Set up viewmodel event
            viewModel.RequestClose += (s, args) => window.Close();

            // Set the data context and show the window
            window.DataContext = viewModel;
            window.Owner = Window.GetWindow(this); // Set the owner window
            window.ShowDialog(); // Show as dialog to block interaction with the main window
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // When the DataContext changes, we need to resubscribe to events
            if (e.OldValue is ChatInterfaceUserControlViewModel oldViewModel)
            {
                oldViewModel.RequestOpenNewChannelWindow -= ViewModel_RequestOpenNewChannelWindow;
                _eventSubscribed = false;
            }

            if (e.NewValue is ChatInterfaceUserControlViewModel newViewModel && !_eventSubscribed)
            {
                newViewModel.RequestOpenNewChannelWindow += ViewModel_RequestOpenNewChannelWindow;
                _eventSubscribed = true;
            }
        }
    }
}