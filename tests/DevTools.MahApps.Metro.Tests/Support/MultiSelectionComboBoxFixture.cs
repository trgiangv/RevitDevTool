using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MahApps.Metro.Controls;

namespace DevTools.MetroFork.Tests.Support;

public sealed class MultiSelectionComboBoxFixture : IDisposable
{
    public LogSettingsComboWindow Window { get; }

    public MultiSelectionComboBox ComboBox => Window.ComboBox;

    public LogSettingsComboViewModel ViewModel { get; }

    public MultiSelectionComboBoxFixture(bool preloadMonitorAndFile = false)
    {
        EnsureApplication();

        ViewModel = new LogSettingsComboViewModel();
        if (preloadMonitorAndFile)
            ViewModel.LoadFromSettings(fileEnabled: true, httpEnabled: false);

        Window = new LogSettingsComboWindow
        {
            DataContext = ViewModel,
            Left = -32000,
            Top = -32000,
        };

        Window.Show();
        Window.UpdateLayout();
        Pump(Window.Dispatcher);
    }

    public static MultiSelectionComboBoxFixture CreatePreloaded() => new(preloadMonitorAndFile: true);

    public static MultiSelectionComboBoxFixture CreateShownEmpty() => new(preloadMonitorAndFile: false);

    public void LoadFromSettings(bool fileEnabled, bool httpEnabled)
    {
        ViewModel.LoadFromSettings(fileEnabled, httpEnabled);
        Window.UpdateLayout();
        Pump(Window.Dispatcher);
    }

    public void Dispose() => Window.Close();

    public static void Pump(Dispatcher dispatcher)
    {
        dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
        dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    public ListBox RequireSelectedItemsPresenter()
    {
        ComboBox.ApplyTemplate();
        var presenter = ComboBox.Template.FindName("PART_SelectedItemsPresenter", ComboBox) as ListBox;
        Assert.IsNotNull(presenter, "PART_SelectedItemsPresenter missing from MultiSelectionComboBox template.");
        return presenter!;
    }

    public ListBox RequirePopupListBox()
    {
        ComboBox.ApplyTemplate();
        var listBox = ComboBox.Template.FindName("PART_PopupListBox", ComboBox) as ListBox;
        Assert.IsNotNull(listBox, "PART_PopupListBox missing from MultiSelectionComboBox template.");
        return listBox!;
    }

    private static void EnsureApplication()
    {
        if (Application.Current is null)
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
    }
}
