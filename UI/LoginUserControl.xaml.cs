using Logic.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace UI
{
    public partial class LoginUserControl : UserControl
    {
        public LoginUserControl()
        {
            InitializeComponent();
            Console.WriteLine("LoginUserControl initialized");

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