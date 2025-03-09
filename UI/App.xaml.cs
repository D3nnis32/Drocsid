using System;
using System.Windows;

namespace UI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Log application start
            Console.WriteLine("Application starting...");

            // Check if resources are loaded correctly
            if (Resources.Count > 0)
            {
                Console.WriteLine($"Application resources loaded: {Resources.Count} resources found");
                foreach (var key in Resources.Keys)
                {
                    Console.WriteLine($"Resource: {key}");
                }
            }
            else
            {
                Console.WriteLine("Warning: No application resources found");
            }

            // Set up a global exception handler
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var exception = args.ExceptionObject as Exception;
                Console.WriteLine($"FATAL ERROR: {exception?.Message}");
                Console.WriteLine($"Stack trace: {exception?.StackTrace}");

                MessageBox.Show($"An unexpected error occurred: {exception?.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }
    }
}