using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using RevitDevTool.Desktop.ViewModels;
using SukiUI;
using SukiUI.Dialogs;
using SukiUI.Models;
using SukiUI.Toasts;

namespace RevitDevTool.Desktop;

public partial class App : Application
{
    public static SukiTheme Theme { get; private set; } = null!;

    /// <summary>
    /// Global dialog manager instance.
    /// </summary>
    public static readonly SukiDialogManager DialogManager = new();

    /// <summary>
    /// Global toast manager instance.
    /// </summary>
    public static readonly SukiToastManager ToastManager = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Theme = SukiTheme.GetInstance();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = Program.AppHost.Services.GetRequiredService<MainWindow>();
            window.DataContext = Program.AppHost.Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ChangeBaseTheme(ThemeVariant variant)
    {
        Theme.ChangeBaseTheme(variant);
    }

    public static void ChangeColorTheme(SukiColorTheme color)
    {
        Theme.ChangeColorTheme(color);
    }

    public static void ChangeColorTheme(string colorName)
    {
        var match = Theme.ColorThemes.FirstOrDefault(c =>
            c.DisplayName.Equals(colorName, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            Theme.ChangeColorTheme(match);
    }
}