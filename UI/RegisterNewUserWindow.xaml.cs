using System.Windows;
using System.Windows.Controls;
using Logic.UI.ViewModels;

namespace UI
{
    public partial class RegisterNewUserWindow : Window
    {
        public RegisterNewUserWindow()
        {
            InitializeComponent();

            // Create or set the ViewModel if you aren't setting it from outside.
            var vm = new RegisterNewUserViewModel();
            vm.RequestClose += (s, e) => this.Close(); // Close the window on success or when commanded
            this.DataContext = vm;
        }

        // Handle PasswordBox changes to set the VM's Password property
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is RegisterNewUserViewModel vm && sender is PasswordBox pb)
            {
                vm.Password = pb.Password;
            }
        }
    }
}
