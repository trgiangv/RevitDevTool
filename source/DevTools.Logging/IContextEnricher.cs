namespace DevTools.Logging;

public interface IContextEnricher
{
    Dictionary<string, object?> GetStaticProperties();
    Dictionary<string, object?>? GetDynamicProperties();
}
