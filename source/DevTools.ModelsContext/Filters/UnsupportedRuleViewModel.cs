namespace DevTools.ModelsContext.Filters;

public sealed class UnsupportedRuleViewModel : FilterNodeViewModel
{
    internal UnsupportedRuleViewModel(IFilterEditor editor, string description)
        : base(editor, true)
    {
        Description = description;
    }

    public string Description { get; }

    internal override FilterNode ToNode()
    {
        return new UnsupportedRule(Description);
    }
}
