using DevTools.Presentation.Views.ModelsContext;
using DevTools.UI.Theme;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace DevTools.ModelsContext.Sandbox;

public partial class SandboxWindow : Window
{
    private SandboxRun? _run;
    private bool _isLoading;
    private bool _isSetting;

    public SandboxWindow(bool autoLoad = true)
    {
        InitializeComponent();
        if (autoLoad)
        {
            Loaded += (_, _) => Reload();
        }
    }

    internal async Task<IReadOnlyList<string>> MeasureAsync(SandboxSize size, Action<string> log)
    {
        SetSize(size);
        return await LoadAsync(log);
    }

    private void SetSize(SandboxSize size)
    {
        _isSetting = true;
        SizeBox.SelectedIndex = (int)size;
        _isSetting = false;
    }

    private void OnSettingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && !_isSetting && e.OriginalSource == sender)
        {
            Reload();
        }
    }

    private void OnReload(object sender, RoutedEventArgs e)
    {
        Reload();
    }

    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        ThemeManager.Current.ApplySettingsTheme(ThemeToggle.IsChecked == true ? AppTheme.Dark : AppTheme.Light);
    }

    private async void Reload()
    {
        try
        {
            await LoadAsync(Log);
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
        }
    }

    private async Task<IReadOnlyList<string>> LoadAsync(Action<string> log)
    {
        if (_isLoading)
        {
            return [];
        }

        _isLoading = true;
        try
        {
            Host.Content = null;
            _run?.Dispose();
            var size = (SandboxSize)SizeBox.SelectedIndex;
            var delay = TimeSpan.FromMilliseconds(int.Parse((string)((ComboBoxItem)DelayBox.SelectedItem).Tag));
            log($"loading {size} with host delay {delay.TotalMilliseconds:F0} ms");
            _run = SandboxRun.Create(size, delay, Dispatcher);
            var view = new ModelContextView { DataContext = _run.Panel };
            Host.Content = view;
            return await _run.MeasureAsync(view, log);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static void Log(string text)
    {
        Console.WriteLine(text);
        Debug.WriteLine(text);
    }
}
