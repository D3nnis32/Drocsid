using Logic.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace UI
{
    public partial class LoginUserControl : UserControl
    {
        private bool _eventSubscribed = false;
        public LoginUserControl()
        {
            InitializeComponent();
            Console.WriteLine("LoginUserControl initialized");

            DataContextChanged += OnDataContextChanged;
            // Debug code to help understand binding issues
            Loaded += (s, e) =>
            {
                Console.WriteLine("LoginUserControl loaded");
                if (DataContext is LoginUserControlViewModel vm)
                {
                    Console.WriteLine("LoginUserControl has correct view model");
                }
                else
                {
                    Console.WriteLine($"WARNING: LoginUserControl has wrong DataContext: {DataContext?.GetType().Name ?? "null"}");
                }
            };
        }
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from the old VM
            if (e.OldValue is LoginUserControlViewModel oldVm)
            {
                oldVm.RequestOpenRegisterWindow -= OnRequestOpenRegisterWindow;
                _eventSubscribed = false;
            }

            // Subscribe to the new VM
            if (e.NewValue is LoginUserControlViewModel newVm && !_eventSubscribed)
            {
                newVm.RequestOpenRegisterWindow += OnRequestOpenRegisterWindow;
                _eventSubscribed = true;
            }
        }

        // This is called when the VM raises RequestOpenRegisterWindow
        private void OnRequestOpenRegisterWindow(object sender, EventArgs e)
        {
            // Create and show the RegisterNewUserWindow
            var window = new RegisterNewUserWindow();

            // Optionally set a data context if needed
            // e.g. window.DataContext = new RegisterNewUserViewModel();

            window.Owner = Window.GetWindow(this); // So the new window is "owned" by the main window
            window.ShowDialog();
        }
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            // Get the PasswordBox that triggered this event
            if (sender is PasswordBox passwordBox && DataContext is LoginUserControlViewModel viewModel)
            {
                // Update the Password property in the ViewModel
                viewModel.Password = passwordBox.Password;
            }
        }
    }
}