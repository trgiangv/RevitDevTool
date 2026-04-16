namespace DevTools.Logging.Abstractions;

public interface IContextEnricher
{
    Dictionary<string, object?> GetStaticProperties();
    Dictionary<string, object?>? GetDynamicProperties();
}
