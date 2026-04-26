using CommunityToolkit.Mvvm.Messaging;
using RevitDevTool.Controllers;
using RevitDevTool.Logging;
using RevitDevTool.Settings;
using DevTools.UI.Theme;
using RevitDevTool.ViewModel.Messages;
// ReSharper disable ReplaceWithFieldKeyword

namespace RevitDevTool.ViewModel.Settings;

public partial class GeneralSettingsViewModel : ObservableValidator, IRecipient<ResetSettingsMessage>
{
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;
    private readonly IMessenger _messenger;

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
    [ObservableProperty] private bool _isMemoryEnabled;

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

    partial void OnIsMemoryEnabledChanged(bool value)
    {
        _settingsService.GeneralConfig.IsMemoryEnabled = value;
        _messenger.Send(new IsMemoryEnableChangedMessage(value));
    }

    public GeneralSettingsViewModel(ISettingsService settingsService, ILoggingService loggingService, IMessenger messenger)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
        _messenger = messenger;
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
        IsMemoryEnabled = _settingsService.GeneralConfig.IsMemoryEnabled;
    }
}
