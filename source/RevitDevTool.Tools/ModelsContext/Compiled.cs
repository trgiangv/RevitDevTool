namespace RevitDevTool.Tools.ModelsContext;

internal abstract record Compiled
{
    public static Compiled All { get; } = new MatchAll();

    public static Compiled None { get; } = new MatchNone();

    public static Compiled And(IEnumerable<Compiled> parts)
    {
        var filters = new List<ElementFilter>();
        foreach (var part in parts)
        {
            switch (part)
            {
                case MatchNone:
                    return None;
                case Filter filter:
                    filters.Add(filter.Value);
                    break;
            }
        }

        return filters.Count switch
        {
            0 => All,
            1 => new Filter(filters[0]),
            _ => new Filter(new LogicalAndFilter(filters))
        };
    }

    public static Compiled Or(IEnumerable<Compiled> parts)
    {
        var filters = new List<ElementFilter>();
        foreach (var part in parts)
        {
            switch (part)
            {
                case MatchAll:
                    return All;
                case Filter filter:
                    filters.Add(filter.Value);
                    break;
            }
        }

        return filters.Count switch
        {
            0 => None,
            1 => new Filter(filters[0]),
            _ => new Filter(new LogicalOrFilter(filters))
        };
    }

    public static Compiled Not(Compiled compiled)
    {
        return compiled switch
        {
            MatchAll => None,
            MatchNone => All,
            Filter filter => new Filter(Invert(filter.Value)),
            _ => compiled
        };
    }

    public bool Passes(Element element)
    {
        return this switch
        {
            MatchAll => true,
            Filter filter => filter.Value.PassesFilter(element),
            _ => false
        };
    }

    private static ElementFilter Invert(ElementFilter filter)
    {
        return filter switch
        {
            ElementParameterFilter parameter => new ElementParameterFilter(parameter.GetRules(), !parameter.Inverted),
            LogicalAndFilter and => new LogicalOrFilter(and.GetFilters().Select(Invert).ToList()),
            LogicalOrFilter or => new LogicalAndFilter(or.GetFilters().Select(Invert).ToList()),
            _ => throw new NotSupportedException(filter.GetType().Name)
        };
    }

    public sealed record MatchAll : Compiled;

    public sealed record MatchNone : Compiled;

    public sealed record Filter(ElementFilter Value) : Compiled;
}
