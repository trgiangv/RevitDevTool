using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.Messaging;
using RevitDevTool.CodeExecute.Providers.Python;
using RevitDevTool.Controllers;
using RevitDevTool.Settings;
using RevitDevTool.Settings.Config;
using RevitDevTool.Theme;
using RevitDevTool.ViewModel.Messages;
// ReSharper disable ReplaceWithFieldKeyword

namespace RevitDevTool.ViewModel.Settings;

public partial class GeneralSettingsViewModel : ObservableValidator, IRecipient<ResetSettingsMessage>
{
    private readonly ISettingsService _settingsService;
    
    private const int MinAllowedPort = 1024;
    private const int MaxAllowedPort = 65535;

    public static List<AppTheme> Themes
    {
        get =>
        [
            AppTheme.Light,
            AppTheme.Dark,
#if REVIT2024_OR_GREATER
            AppTheme.Auto
#endif
        ];
    }

    [ObservableProperty] private AppTheme _theme;
    [ObservableProperty] private bool _useHardwareRendering;
    [ObservableProperty] private bool _isDebuggerConnected;
    
    private int _debugPort;
    [Range(MinAllowedPort, MaxAllowedPort, ErrorMessage = "Port number must be between 1024 and 65535.")]
    public int DebugPort
    {
        get => _debugPort;
        set
        {
            if (SetProperty(ref _debugPort, value, true))
            {
                OnDebugPortChanged(value);
            }
        }
    }

    partial void OnThemeChanged(AppTheme value)
    {
        _settingsService.GeneralConfig.Theme = value;
        ThemeManager.Current.ApplySettingsTheme(value);
    }

    partial void OnUseHardwareRenderingChanged(bool value)
    {
        _settingsService.GeneralConfig.UseHardwareRendering = value;
        HostBackgroundController.ToggleHardwareRendering(_settingsService);
    }

    private void OnDebugPortChanged(int value)
    {
        _settingsService.GeneralConfig.DebugPort = value;
        PythonInitializer.ListenToDebugger();
    }
    
    [UsedImplicitly]
    public void RevertIfInvalid()
    {
        if (!GetErrors(nameof(DebugPort)).Any()) return;
        DebugPort = GeneralConfig.DefaultDebugPort;
        ClearErrors(nameof(DebugPort));
        OnPropertyChanged(nameof(DebugPort));
    }

    public GeneralSettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadFromConfig();
        WeakReferenceMessenger.Default.Register(this);
    }

    public void Receive(ResetSettingsMessage message)
    {
        LoadFromConfig();
    }

    private void LoadFromConfig()
    {
        Theme = _settingsService.GeneralConfig.Theme;
        UseHardwareRendering = _settingsService.GeneralConfig.UseHardwareRendering;
        DebugPort = _settingsService.GeneralConfig.DebugPort is >= MinAllowedPort and <= MaxAllowedPort
            ? _settingsService.GeneralConfig.DebugPort
            : GeneralConfig.DefaultDebugPort;
    }
}
