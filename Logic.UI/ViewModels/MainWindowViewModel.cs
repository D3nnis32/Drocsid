using System;
using System.ComponentModel;

namespace Logic.UI.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private object _currentView;

        public object CurrentView
        {
            get => _currentView;
            set
            {
                _currentView = value;
                OnPropertyChanged(nameof(CurrentView));
            }
        }

        public MainWindowViewModel()
        {
            // Start with the login view
            var loginViewModel = new LoginUserControlViewModel();
            loginViewModel.LoginSuccessful += OnLoginSuccessful;
            CurrentView = loginViewModel;

            // For debugging, print what view we're starting with
            Console.WriteLine($"DEBUG: Initial view set to {CurrentView?.GetType().Name}");
        }

        private void OnLoginSuccessful(object sender, EventArgs e)
        {
            // Switch to the chat interface view
            var chatViewModel = new ChatInterfaceUserControlViewModel();
            CurrentView = chatViewModel;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}