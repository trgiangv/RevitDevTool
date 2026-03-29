using CommunityToolkit.Mvvm.Messaging;
using RevitDevTool.Controllers;
using RevitDevTool.Logging;
using RevitDevTool.Settings;
using RevitDevTool.Theme;
using RevitDevTool.ViewModel.Messages;
// ReSharper disable ReplaceWithFieldKeyword

namespace RevitDevTool.ViewModel.Settings;

public partial class GeneralSettingsViewModel : ObservableValidator, IRecipient<ResetSettingsMessage>
{
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;

    public static List<AppTheme> Themes =>
    [
        AppTheme.Light,
        AppTheme.Dark,
#if REVIT2024_OR_GREATER
        AppTheme.Auto
#endif
    ];

    [ObservableProperty] private AppTheme _theme;
    [ObservableProperty] private bool _useHardwareRendering;

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

    public GeneralSettingsViewModel(ISettingsService settingsService, ILoggingService loggingService, IMessenger messenger)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
        LoadFromConfig();
        messenger.Register(this);
        ThemeManager.Current.ActualApplicationThemeChanged += OnActualThemeChanged;
    }

    public void Receive(ResetSettingsMessage message)
    {
        LoadFromConfig();
    }

    private void OnActualThemeChanged(object? sender, EventArgs e)
    {
        _loggingService.SetTheme(ThemeManager.Current.ActualApplicationTheme == AppTheme.Dark);
    }

    private void LoadFromConfig()
    {
        Theme = _settingsService.GeneralConfig.Theme;
        UseHardwareRendering = _settingsService.GeneralConfig.UseHardwareRendering;
    }
}
