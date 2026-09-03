using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using DevTools.Daemon.Desktop;
using DevTools.Settings.Configs;

namespace DevTools.Daemon.Views;

public sealed class SettingsView(Preferences preferences, string version) : UserControl
{
    protected override Element OnBuild() =>
        new StackPanel()
            .Vertical()
            .Margin(16)
            .Children(
                Heading("Appearance"),
                new ComboBox()
                    .Width(140)
                    .Left()
                    .Margin(0, 0, 0, 24)
                    .Items(preferences.Themes, theme => theme.ToString())
                    .BindSelectedIndex(
                        preferences.Theme,
                        theme => (int)theme,
                        index => (AppTheme)index),
                Heading("Startup"),
                new CheckBox()
                    .Content("Start DevTools Daemon at login")
                    .Margin(0, 0, 0, 24)
                    .BindIsChecked(preferences.AutoStartEnabled),
                Heading("About"),
                new TextBlock().Text($"Version {version}"));

    private static TextBlock Heading(string text) =>
        new TextBlock()
            .Text(text)
            .FontSize(16)
            .Bold()
            .Margin(0, 0, 0, 8);
}
