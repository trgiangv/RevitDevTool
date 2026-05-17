using RevitDevTool.CommandBrowser.Models;

namespace RevitDevTool.CommandBrowser.Controls;

/// <summary>
/// Custom filter that searches across Name, TabName, and Description.
/// </summary>
public sealed class CommandBrowserFilterSetting : AutoCompleteComboBoxSetting
{
    public override Predicate<object> GetFilter(string query, Func<object, string> stringFromItem)
    {
        if (string.IsNullOrWhiteSpace(query))
            return _ => true;

        return item =>
        {
            if (item is not GroupedCommandEntry entry) return false;

            var info = entry.Command.RibbonInfo;
            return info.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || info.TabName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || info.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
        };
    }

    public override int MaxSuggestionCount => 100;

    public override TimeSpan Delay => TimeSpan.FromMilliseconds(300);
}
