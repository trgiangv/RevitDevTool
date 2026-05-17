namespace RevitDevTool.CommandBrowser.Controls;

/// <summary>
/// Configuration for <see cref="AutoCompleteComboBox"/> filtering and dropdown behavior.
/// Ported from DotNetKit.Wpf.AutoCompleteComboBox.
/// </summary>
public class AutoCompleteComboBoxSetting
{
    /// <summary>
    /// Gets a filter predicate for the given query text.
    /// Default: case-insensitive contains match.
    /// </summary>
    public virtual Predicate<object> GetFilter(string query, Func<object, string> stringFromItem)
    {
        return item => stringFromItem(item).Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Maximum number of suggestions before the dropdown auto-opens.
    /// Higher values increase filtering cost.
    /// </summary>
    public virtual int MaxSuggestionCount => 100;

    /// <summary>
    /// Delay before updating the suggestion list after typing. Zero = immediate.
    /// </summary>
    public virtual TimeSpan Delay => TimeSpan.FromMilliseconds(300);

    private static AutoCompleteComboBoxSetting _default = new();

    public static AutoCompleteComboBoxSetting Default
    {
        get => _default;
        set => _default = value ?? throw new ArgumentNullException(nameof(value));
    }
}
