namespace RevitMcpToolSet.Utilities;

public static class ElementIdExtensions
{
    public static long ToValue(this ElementId id)
    {
#if REVIT2024_OR_GREATER
        return id.Value;
#else
        return id.IntegerValue;
#endif
    }
    
    public static ElementId ToElementId(this long id)
    {
#if REVIT2024_OR_GREATER
        return new ElementId(id);
#else
        return new ElementId((int)id);
#endif
    }
}
