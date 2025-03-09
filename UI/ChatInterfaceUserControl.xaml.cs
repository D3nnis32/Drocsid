using Logic.UI.ViewModels;
using System.Windows.Controls;
using System.Windows;

namespace UI
{
    public partial class ChatInterfaceUserControl : UserControl
    {
        public ChatInterfaceUserControl()
        {
            InitializeComponent();

            // Subscribe to the view model's events
            if (DataContext is ChatInterfaceUserControlViewModel viewModel)
            {
                viewModel.RequestOpenNewChannelWindow += ViewModel_RequestOpenNewChannelWindow;
            }
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
            window.Owner = System.Windows.Window.GetWindow(this); // Set the owner window
            window.ShowDialog(); // Show as dialog to block interaction with the main window
        }

        private void UserControl_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            // When the DataContext changes, we need to resubscribe to events
            if (e.OldValue is ChatInterfaceUserControlViewModel oldViewModel)
            {
                oldViewModel.RequestOpenNewChannelWindow -= ViewModel_RequestOpenNewChannelWindow;
            }

            if (e.NewValue is ChatInterfaceUserControlViewModel newViewModel)
            {
                newViewModel.RequestOpenNewChannelWindow += ViewModel_RequestOpenNewChannelWindow;
            }
        }
    }
}