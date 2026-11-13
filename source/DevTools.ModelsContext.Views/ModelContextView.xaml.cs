using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace DevTools.ModelsContext.Views;

public partial class ModelContextView : UserControl
{
    private ModelContextViewModel? _panel;
    private bool _isSyncing;

    public ModelContextView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_panel is not null)
        {
            _panel.ContextBar.PropertyChanged -= OnContextBarChanged;
            _panel.ContextBar.Documents.CollectionChanged -= OnDocumentsChanged;
            _panel.ContextMenu.PickColorAsync = null;
        }

        _panel = e.NewValue as ModelContextViewModel;
        if (_panel is null)
        {
            return;
        }

        _panel.ContextBar.PropertyChanged += OnContextBarChanged;
        _panel.ContextBar.Documents.CollectionChanged += OnDocumentsChanged;
        _panel.ContextMenu.PickColorAsync = PickColorAsync;
        Tabs.SelectedIndex = (int)_panel.SelectedTab;
        SyncDocuments();
    }

    private void OnContextBarChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ContextBarViewModel.Summary))
        {
            SyncDocuments();
        }
    }

    private void OnDocumentsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(SyncDocuments), System.Windows.Threading.DispatcherPriority.DataBind);
    }

    private void OnDocumentsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncing || _panel is null)
        {
            return;
        }

        var selected = DocumentsBox.SelectedItems.Cast<DocumentItemViewModel>().ToHashSet();
        _isSyncing = true;
        try
        {
            foreach (var item in _panel.ContextBar.Documents.ToList())
            {
                item.IsChecked = selected.Contains(item);
            }
        }
        finally
        {
            _isSyncing = false;
        }

        SyncDocuments();
    }

    private void SyncDocuments()
    {
        if (_isSyncing || _panel is null)
        {
            return;
        }

        var wanted = _panel.ContextBar.Documents.Where(item => item.IsChecked).ToHashSet();
        var current = DocumentsBox.SelectedItems.Cast<DocumentItemViewModel>().ToHashSet();
        if (wanted.SetEquals(current))
        {
            return;
        }

        _isSyncing = true;
        try
        {
            DocumentsBox.SelectedItems.Clear();
            foreach (var item in wanted)
            {
                DocumentsBox.SelectedItems.Add(item);
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource != Tabs || _panel is null || Tabs.SelectedIndex < 0)
        {
            return;
        }

        _panel.SelectedTab = (PanelTab)Tabs.SelectedIndex;
    }

    private Task<RgbColor?> PickColorAsync()
    {
        var dialog = new ColorPickDialog { Owner = Window.GetWindow(this) };
        var result = dialog.ShowDialog() == true && dialog.Picked is { } color
            ? new RgbColor(color.R, color.G, color.B)
            : (RgbColor?)null;
        return Task.FromResult(result);
    }
}
