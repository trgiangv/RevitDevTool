using RevitDevTool.Settings;
using RevitDevTool.Settings.Config;
// ReSharper disable UnusedParameterInPartialMethod
namespace RevitDevTool.ViewModel.Execute;

/// <summary>
/// Coordinator ViewModel that manages mode switching between CSharp and Python execution
/// </summary>
public partial class CodeExecuteViewModel : ObservableObject
{
    private readonly CSharpExecuteViewModel _csharpViewModel;
    private readonly PythonExecuteViewModel _pythonViewModel;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private Contracts.ExecuteViewModelBase _activeViewModel = null!;

    [ObservableProperty]
    private ExecutionMode _currentMode;

    public bool IsCSharpMode => CurrentMode == ExecutionMode.CSharp;
    public bool IsPythonMode => CurrentMode == ExecutionMode.Python;

    public CodeExecuteViewModel(
        CSharpExecuteViewModel csharpViewModel,
        PythonExecuteViewModel pythonViewModel,
        ISettingsService settingsService)
    {
        _csharpViewModel = csharpViewModel;
        _pythonViewModel = pythonViewModel;
        _settingsService = settingsService;
        CurrentMode = settingsService.CodeExecuteConfig.ExecutionMode;
        ActiveViewModel = IsCSharpMode ? _csharpViewModel : _pythonViewModel;
    }

    /// <summary>
    /// Switch between CSharp and Python execution modes
    /// </summary>
    [RelayCommand]
    private void SwitchMode()
    {
        CurrentMode = IsCSharpMode ? ExecutionMode.Python : ExecutionMode.CSharp;
        ActiveViewModel = IsCSharpMode ? _csharpViewModel : _pythonViewModel;
        _settingsService.CodeExecuteConfig.ExecutionMode = CurrentMode;
    }

    /// <summary>
    /// Execute the last executed item from the active ViewModel
    /// </summary>
    public void ExecuteLastItem()
    {
        ActiveViewModel.ExecuteLastItem();
    }

    partial void OnCurrentModeChanged(ExecutionMode value)
    {
        OnPropertyChanged(nameof(IsCSharpMode));
        OnPropertyChanged(nameof(IsPythonMode));
    }
}
