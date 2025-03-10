using Logic.UI.ViewModels;
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
                _eventSubscribed = false;
            }

            if (e.NewValue is ChatViewModel newViewModel && !_eventSubscribed)
            {
                newViewModel.RequestOpenAddMembersWindow += ChatViewModel_RequestOpenAddMembersWindow;
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
    }
}
