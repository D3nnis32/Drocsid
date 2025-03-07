using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Logic.UI
{
    public class RelayCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        private readonly Action<object>? parameterMethodToExecute;
        private readonly Action methodToExecute;
        private readonly Func<bool>? canExecuteEvaluator;

        public RelayCommand(Action methodToExecute, Func<bool>? canExecuteEvaluator = null)
        {
            this.methodToExecute = methodToExecute ?? throw new ArgumentNullException(nameof(methodToExecute));
            this.canExecuteEvaluator = canExecuteEvaluator;
        }

        public RelayCommand(Action<object> methodToExecute, Func<bool>? canExecuteEvaluator = null)
        {
            this.parameterMethodToExecute = methodToExecute ?? throw new ArgumentNullException(nameof(methodToExecute));
            this.methodToExecute = () => methodToExecute(null!);
            this.canExecuteEvaluator = canExecuteEvaluator;
        }

        public bool CanExecute(object? parameter)
        {
            return canExecuteEvaluator?.Invoke() ?? true;
        }

        public void Execute(object? parameter)
        {
            if (methodToExecute != null)
            {
                methodToExecute.Invoke();
            }
            if (parameterMethodToExecute != null)
            {
                parameterMethodToExecute.Invoke(parameter!);
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}