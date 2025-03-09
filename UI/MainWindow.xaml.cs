using Logic.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Console.WriteLine("MainWindow initialized");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("MainWindow loaded");

            // Print some debug info about the current view
            if (DataContext is MainWindowViewModel viewModel)
            {
                Console.WriteLine($"Current view is: {viewModel.CurrentView?.GetType().Name ?? "null"}");

                // Force visibility of the login control if there's an issue
                if (viewModel.CurrentView == null)
                {
                    Console.WriteLine("WARNING: CurrentView is null, setting to LoginUserControlViewModel");
                    viewModel.CurrentView = new LoginUserControlViewModel();
                }
            }
            else
            {
                Console.WriteLine("ERROR: DataContext is not MainWindowViewModel");
            }

            // Check if we can find LoginUserControl in resources
            foreach (var key in Application.Current.Resources.Keys)
            {
                Console.WriteLine($"Resource: {key}");
                if (Application.Current.Resources[key] is DataTemplate dt)
                {
                    Console.WriteLine($"  - DataTemplate for: {dt.DataType}");
                }
            }
        }
    }
}