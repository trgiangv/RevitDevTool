using CommunityToolkit.Mvvm.Messaging;
using DevTools.Presentation.Interfaces;
using DevTools.Presentation.ViewModels.Messages;
using DevTools.UI.Theme;
using DevTools.Utilities;
namespace DevTools.Presentation.ViewModels.Settings;

public partial class GeneralSettingsViewModel : ObservableValidator, IRecipient<ResetSettingsMessage>
{
    private readonly IDevToolsSettingsService _settingsService;
    private readonly IDevToolsLoggingService _loggingService;
    private readonly IMessenger _messenger;

    public static List<AppTheme> Themes => [AppTheme.Light, AppTheme.Dark, AppTheme.Auto];

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
        DispatcherHelper.ToggleHardwareRendering(value);
    }

    partial void OnIsMemoryEnabledChanged(bool value)
    {
        _settingsService.GeneralConfig.IsMemoryEnabled = value;
        _messenger.Send(new IsMemoryEnableChangedMessage(value));
    }

    public GeneralSettingsViewModel(
        IDevToolsSettingsService settingsService,
        IDevToolsLoggingService loggingService,
        IMessenger messenger)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
        _messenger = messenger;
        LoadFromConfig();
        messenger.Register(this);
        ThemeManager.Current.ActualApplicationThemeChanged += OnActualThemeChanged;
    }

    public void Receive(ResetSettingsMessage message) => LoadFromConfig();

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
