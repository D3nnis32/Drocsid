using System;
using System.Windows.Input;

namespace Logic.UI.ViewModels
{
    // Static class to access CommandManager
    public static class CommandManager
    {
        public static void InvalidateRequerySuggested()
        {
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _executeWithParam;
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        // Constructor for commands with no parameter
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _executeWithParam = null;
            _canExecute = canExecute;
        }

        // Constructor for commands with parameter
        public RelayCommand(Action<object> execute, Func<bool> canExecute = null)
        {
            _executeWithParam = execute ?? throw new ArgumentNullException(nameof(execute));
            _execute = null;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            if (_execute != null)
            {
                _execute();
            }
            else if (_executeWithParam != null)
            {
                _executeWithParam(parameter);
            }
        }

        public event EventHandler CanExecuteChanged
        {
            add { System.Windows.Input.CommandManager.RequerySuggested += value; }
            remove { System.Windows.Input.CommandManager.RequerySuggested -= value; }
        }

        // Add this method to allow manual triggering of CanExecute evaluation
        public void InvalidateCanExecute()
        {
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    // Generic version for strongly-typed parameters
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (parameter == null && typeof(T).IsValueType)
                return false;

            return _canExecute == null || _canExecute((T)parameter);
        }

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { System.Windows.Input.CommandManager.RequerySuggested += value; }
            remove { System.Windows.Input.CommandManager.RequerySuggested -= value; }
        }

        public void InvalidateCanExecute()
        {
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }
}