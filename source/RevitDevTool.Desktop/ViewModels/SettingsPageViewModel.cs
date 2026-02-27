using System.ComponentModel;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;

namespace RevitDevTool.Desktop.ViewModels;

public partial class SettingsPageViewModel : PageViewModelBase
{
    public override int Index => 3;
    public override string DisplayName => "Settings";
    public override MaterialIconKind Icon => MaterialIconKind.Cog;

    // Theme - base
    [ObservableProperty] private bool _isSystemTheme = true;
    [ObservableProperty] private bool _isLightTheme;
    [ObservableProperty] private bool _isDarkTheme;

    // Theme - color swatches
    [ObservableProperty] private bool _isColorBlue = true;
    [ObservableProperty] private bool _isColorPurple;
    [ObservableProperty] private bool _isColorGreen;
    [ObservableProperty] private bool _isColorOrange;
    [ObservableProperty] private bool _isColorRed;
    [ObservableProperty] private bool _isColorPink;

    // Background
    [ObservableProperty] private bool _backgroundAnimations = true;
    [ObservableProperty] private bool _backgroundTransitions = true;

    // General settings
    [ObservableProperty] private bool _checkForUpdatesOnStart = true;
    [ObservableProperty] private bool _enableNotifications = true;
    [ObservableProperty] private bool _autoSaveProfile = true;

    // Processor settings
    [ObservableProperty] private decimal _defaultParallelCount = 2;
    [ObservableProperty] private string _defaultRevitVersion = "2025";
    [ObservableProperty] private decimal _maxLogLines = 1000;

    // Paths
    [ObservableProperty] private string _defaultConfigPath = string.Empty;

    public IReadOnlyList<string> RevitVersions { get; } = ["2024", "2025", "2026"];

    public SettingsPageViewModel()
    {
        IsVisibleOnSideMenu = false;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(IsSystemTheme) when IsSystemTheme:
                App.ChangeBaseTheme(ThemeVariant.Default);
                break;
            case nameof(IsLightTheme) when IsLightTheme:
                App.ChangeBaseTheme(ThemeVariant.Light);
                break;
            case nameof(IsDarkTheme) when IsDarkTheme:
                App.ChangeBaseTheme(ThemeVariant.Dark);
                break;
            case nameof(IsColorBlue) when IsColorBlue:
                App.ChangeColorTheme("Blue");
                break;
            case nameof(IsColorPurple) when IsColorPurple:
                App.ChangeColorTheme("Purple");
                break;
            case nameof(IsColorGreen) when IsColorGreen:
                App.ChangeColorTheme("Green");
                break;
            case nameof(IsColorOrange) when IsColorOrange:
                App.ChangeColorTheme("Orange");
                break;
            case nameof(IsColorRed) when IsColorRed:
                App.ChangeColorTheme("Red");
                break;
            case nameof(IsColorPink) when IsColorPink:
                App.ChangeColorTheme("Pink");
                break;
        }
    }

    [RelayCommand]
    private void BrowseDefaultConfigPath() { }

    [RelayCommand]
    private void SaveSettings() { }
}
