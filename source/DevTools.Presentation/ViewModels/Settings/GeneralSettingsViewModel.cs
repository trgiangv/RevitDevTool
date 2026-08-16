using CommunityToolkit.Mvvm.Messaging;
using DevTools.Hosting;
using DevTools.Presentation.Interfaces;
using DevTools.Settings;
using DevTools.Presentation.ViewModels.Messages;
using DevTools.UI.Theme;
using DevTools.Utilities;
namespace DevTools.Presentation.ViewModels.Settings;

public partial class GeneralSettingsViewModel : ObservableValidator, IRecipient<ResetSettingsMessage>
{
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;
    private readonly IMessenger _messenger;
    private readonly IHostAppInfo _hostAppInfo;

    public List<AppTheme> Themes
    {
        get
        {
            var themes = Enum.GetValues<AppTheme>().ToList();
            // Revit 2023 and earlier do not support Dark theme
            if (_hostAppInfo.Host == HostApp.Revit && int.Parse(_hostAppInfo.VersionNumber) < 2024)
            {
                themes.Remove(AppTheme.Auto);
            }
            return themes;
        }
    }

    [ObservableProperty]
    public partial AppTheme Theme { get; set; }

    [ObservableProperty]
    public partial bool UseHardwareRendering { get; set; }

    [ObservableProperty]
    public partial bool IsMemoryEnabled { get; set; }

    [ObservableProperty]
    public partial bool EnableTelemetry { get; set; }

    partial void OnThemeChanged(AppTheme value)
    {
        _settingsService.GeneralConfig.Theme = value;
        ThemeManager.Current.ApplySettingsTheme(value);
    }

    partial void OnUseHardwareRenderingChanged(bool value)
    {
        _settingsService.GeneralConfig.UseHardwareRendering = value;
        HostUiHelper.ToggleHardwareRendering(value);
    }

    partial void OnIsMemoryEnabledChanged(bool value)
    {
        _settingsService.GeneralConfig.IsMemoryEnabled = value;
        _messenger.Send(new IsMemoryEnableChangedMessage(value));
    }

    partial void OnEnableTelemetryChanged(bool value)
    {
        _settingsService.GeneralConfig.EnableTelemetry = value;
    }

    public GeneralSettingsViewModel(
        IHostAppInfo hostAppInfo,
        ISettingsService settingsService,
        ILoggingService loggingService,
        IMessenger messenger)
    {
        _hostAppInfo = hostAppInfo;
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
        EnableTelemetry = _settingsService.GeneralConfig.EnableTelemetry;
    }
}
