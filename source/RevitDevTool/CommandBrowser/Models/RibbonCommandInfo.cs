using System.Text.RegularExpressions;
using System.Windows.Media;
using Autodesk.Windows;
using RevitDevTool.Core;
using RibbonPanel = Autodesk.Windows.RibbonPanel;

namespace RevitDevTool.CommandBrowser.Models;

/// <summary>
/// DTO wrapping an Autodesk ribbon command item and its parent panel.
/// Extracts display-ready properties once during snooping so the UI never touches raw ribbon internals.
/// </summary>
public sealed partial class RibbonCommandInfo : ObservableObject
{
    private readonly RibbonCommandItem _commandItem;
    private readonly RibbonPanel _panel;
    private readonly RevitCommandId? _revitCommandId;

    [ObservableProperty]
    public partial ImageSource? Image { get; private set; } = null;

    [ObservableProperty]
    public partial bool IsEnabled { get; private set; }

    public RibbonCommandInfo(RibbonCommandItem commandItem, RibbonPanel panel)
    {
        _commandItem = commandItem;
        _panel = panel;
        Id = commandItem.Id;
        Name = BuildName();
        FullName = BuildFullName();
        Description = ExtractDescription();
        Image = commandItem.Image ?? commandItem.LargeImage;

        _revitCommandId = RevitCommandId.LookupCommandId(Id);
        if (_revitCommandId is not null)
            IsEnabled = RevitContext.UiApplication.CanPostCommand(_revitCommandId);
    }

    public string Id { get; }
    public string Name { get; }
    private string FullName { get; }
    public string Description { get; }
    public string TabName => _panel.Tab.Title;

    public string ToolTip => field ??= string.IsNullOrEmpty(Description)
        ? FullName
        : $"{FullName}{Environment.NewLine}{Environment.NewLine}{Description}";

    public void RefreshImage()
    {
        Image = _commandItem.Image ?? _commandItem.LargeImage;
    }

    /// <summary>
    /// Re-evaluates command availability via <see cref="Autodesk.Revit.UI.UIApplication.CanPostCommand"/>.
    /// Called when the dropdown opens so the UI reflects the current Revit context.
    /// </summary>
    public void RefreshIsEnabled()
    {
        if (_revitCommandId is null) return;
        IsEnabled = RevitContext.UiApplication.CanPostCommand(_revitCommandId);
    }

    private string BuildName()
    {
        var name = _commandItem.Name;
        var text = _commandItem.Text;
        var hasName = !string.IsNullOrEmpty(name);
        var hasText = !string.IsNullOrEmpty(text);

        return (hasName, hasText) switch
        {
            (true, true) => $"{Sanitize(text)} - {Sanitize(name)}",
            (true, false) => Sanitize(name),
            _ => Sanitize(text)
        };
    }

    private string BuildFullName() => $"{_panel.Tab.Title} -> {_panel.Source.Title} -> {Name}";

    private string ExtractDescription()
    {
        if (_commandItem.ToolTip is not RibbonToolTip ribbonToolTip) return string.Empty;

        return ribbonToolTip.Content switch
        {
            System.Windows.Controls.TextBlock tb => tb.Text,
            string s => s,
            _ => string.Empty
        };
    }

#if NET7_0_OR_GREATER
    [GeneratedRegex(@"\r\n|\t|\n|\r")]
    private static partial Regex WhitespacePattern();
#else
    private static readonly Regex _whitespacePattern = new(@"\r\n|\t|\n|\r", RegexOptions.Compiled);
    private static Regex WhitespacePattern() => _whitespacePattern;
#endif

    private static string Sanitize(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : WhitespacePattern().Replace(value, " ");
}
