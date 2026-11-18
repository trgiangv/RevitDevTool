using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.ViewModels;
using HandyControl.Data;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DevTools.Presentation.Views.ModelsContext;

public partial class ModelContextView : UserControl
{
    private const string FilterViewName = "FilterView";

    private FrameworkElement? _colorAnchor;
    private ModelContextViewModel? _panel;
    private bool _isSyncing;

    public ModelContextView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_panel is not null)
        {
            _panel.IsPaneVisible = (bool)e.NewValue;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_panel is not null)
        {
            _panel.PropertyChanged -= OnPanelChanged;
            _panel.ContextBar.PropertyChanged -= OnContextBarChanged;
            _panel.ContextBar.Documents.CollectionChanged -= OnDocumentsChanged;
            _panel.ContextMenu.ColorRequested -= OnColorRequested;
        }

        _panel = e.NewValue as ModelContextViewModel;
        if (_panel is null)
        {
            return;
        }

        _panel.PropertyChanged += OnPanelChanged;
        _panel.ContextBar.PropertyChanged += OnContextBarChanged;
        _panel.ContextBar.Documents.CollectionChanged += OnDocumentsChanged;
        _panel.ContextMenu.ColorRequested += OnColorRequested;
        SyncDocuments();
        EnsureFilterView();
    }

    private void OnPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModelContextViewModel.IsFilterTab))
        {
            EnsureFilterView();
        }
    }

    // Built on first use: it is about a third of the panel's first build.
    private void EnsureFilterView()
    {
        if (_panel is not { IsFilterTab: true } || FindName(FilterViewName) is not null)
        {
            return;
        }

        var view = new FilterTabView { Style = (Style)FindResource("FilterBodyStyle") };
        System.Windows.Automation.AutomationProperties.SetAutomationId(view, "ModelContext.Filter");
        Body.Children.Add(view);
        RegisterName(FilterViewName, view);
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
            foreach (var item in _panel.ContextBar.Documents.Where(item => item.IsEnabled).Reverse().OrderByDescending(selected.Contains).ToList())
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

    internal void SetColorAnchor(FrameworkElement? anchor)
    {
        _colorAnchor = anchor;
    }

    private void OnColorRequested(object? sender, EventArgs e)
    {
        if (_colorAnchor is { IsVisible: true } anchor)
        {
            ColorPopup.PlacementTarget = anchor;
            ColorPopup.Placement = PlacementMode.Bottom;
        }
        else
        {
            ColorPopup.PlacementTarget = null;
            ColorPopup.Placement = PlacementMode.MousePoint;
        }

        _colorAnchor = null;
        ColorPopup.IsOpen = true;
    }

    private void OnColorConfirmed(object sender, FunctionEventArgs<Color> e)
    {
        ColorPopup.IsOpen = false;
        if (_panel is { } panel)
        {
            panel.ContextMenu.ApplyColorCommand.Execute(new RgbColor(e.Info.R, e.Info.G, e.Info.B));
        }
    }

    private void OnColorCanceled(object? sender, EventArgs e)
    {
        ColorPopup.IsOpen = false;
    }
}
