using System.Collections.ObjectModel;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI.Events;
using RevitDevTool.Core;
using RevitDevTool.Tools.CommandBrowser;
using RevitDevTool.Tools.Helpers;
using RevitDevTool.Tools.Selection;

namespace RevitDevTool.Tools.ElementFinder;

public sealed partial class ElementFinderViewModel(ToolWindowService toolWindows) : ObservableObject
{
    private const string WindowKey = "ElementFinder";

    public ObservableCollection<DocumentItem> Documents { get; } = [];

    [ObservableProperty]
    public partial DocumentItem? SelectedDocument { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    public void Show()
    {
        if (IsOpen)
            return;

        toolWindows.Show(WindowKey, () =>
        {
            RefreshDocuments();
            Subscribe();

            var window = new ElementFinderView { DataContext = this };
            window.Closed += (_, _) =>
            {
                Unsubscribe();
                IsOpen = false;
            };
            IsOpen = true;
            return window;
        });
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        toolWindows.Close(WindowKey);
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Show();
    }

    public void RefreshDocuments()
    {
        Documents.Clear();
        var uiDoc = RevitContext.ActiveUiDocument;
        var doc = uiDoc?.Document;
        if (doc is null)
        {
            SelectedDocument = null;
            return;
        }

        Documents.Add(new DocumentItem(doc));

        var links = new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .Where(li => li.GetLinkDocument() is not null);

        foreach (var link in links)
        {
            var linkDoc = link.GetLinkDocument();
            if (linkDoc is not null)
                Documents.Add(new DocumentItem(linkDoc, link));
        }

        SelectedDocument = Documents.FirstOrDefault();
    }

    [RelayCommand]
    private void SelectOnly() => RunAction(zoom: false, sectionBox: false);

    [RelayCommand]
    private void SelectAndZoom() => RunAction(zoom: true, sectionBox: false);

    [RelayCommand]
    private void SelectZoomSectionBox() => RunAction(zoom: true, sectionBox: true);

    [RelayCommand]
    private void CopyElementIds() => CopySelected(TokenKind.ElementId);

    [RelayCommand]
    private void CopyUniqueIds() => CopySelected(TokenKind.UniqueId);

    [RelayCommand]
    private void CopyIfcGuids() => CopySelected(TokenKind.IfcGuid);

    [RelayCommand]
    private void Refresh() => RefreshDocuments();

    private void CopySelected(TokenKind kind)
    {
        var uiDoc = RevitContext.ActiveUiDocument;
        if (uiDoc is null)
        {
            StatusText = "No active document";
            return;
        }

        var text = ElementFinderService.FormatSelected(uiDoc, kind);
        if (string.IsNullOrEmpty(text))
        {
            StatusText = kind == TokenKind.IfcGuid
                ? "No IFC Guid on selection"
                : "Nothing selected";
            return;
        }

        System.Windows.Clipboard.SetText(text);
        StatusText = $"Copied {ElementFinderService.ParseTokens(text).Count} {kind}(s)";
    }

    private void Subscribe()
    {
        RevitContextExecutor.Raise(() =>
        {
            RevitContext.UiApplication.ViewActivated += OnViewActivated;
            RevitContext.Application.DocumentOpened += OnDocumentOpened;
        });
    }

    private void Unsubscribe()
    {
        RevitContextExecutor.Raise(() =>
        {
            RevitContext.UiApplication.ViewActivated -= OnViewActivated;
            RevitContext.Application.DocumentOpened -= OnDocumentOpened;
        });
    }

    private void OnViewActivated(object? sender, ViewActivatedEventArgs e) => RefreshDocuments();

    private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e) => RefreshDocuments();

    private void RunAction(bool zoom, bool sectionBox)
    {
        var matches = ResolveMatches();
        if (matches.Count == 0)
        {
            StatusText = "No matches";
            return;
        }

        ElementSelector.Select(matches, zoom, sectionBox);
        StatusText = $"{ElementSelector.CountElements(matches)} element(s)";
    }

    private IReadOnlyList<SearchMatch> ResolveMatches()
    {
        if (SelectedDocument is not { } item)
            return [];
        var hostDocument = item.IsLinked ? item.LinkInstance!.Document : item.Document;
        var defaultLinkId = item.IsLinked ? TokenParser.FormatElementId(item.LinkInstance!.Id) : null;
        return ElementSearcher.SearchTokens(hostDocument, ElementFinderService.ParseTokens(SearchText), defaultLinkId);
    }
}
