using System.Windows.Input;

namespace RevitDevTool.UI.Binding;

public class DelegateCommand : ICommand
{
    public Action<object> ExecuteCommand { get; set; }

    public Func<object, bool> CanExecuteCommand { get; set; }

    public DelegateCommand() { }

    public DelegateCommand(Action<object> execute, Func<object, bool> canExecute)
    {
        ExecuteCommand = execute;
        CanExecuteCommand = canExecute;
    }

    public bool CanExecute(object parameter)
    {
        if (CanExecuteCommand != null)
        {
            return CanExecuteCommand(parameter);
        }
        else
        {
            return true;
        }
    }

    public event EventHandler CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public void Execute(object parameter)
    {
        ExecuteCommand(parameter);
    }
}