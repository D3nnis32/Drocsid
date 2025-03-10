using Logic.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace UI
{
    public partial class ChatUserControl : UserControl
    {
        private bool _eventSubscribed = false;

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
                _eventSubscribed = false;
            }

            if (e.NewValue is ChatViewModel newViewModel && !_eventSubscribed)
            {
                newViewModel.RequestOpenAddMembersWindow += ChatViewModel_RequestOpenAddMembersWindow;
                newViewModel.RequestShowPluginsWindow += ViewModel_RequestShowPluginsWindow;
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
    }
}