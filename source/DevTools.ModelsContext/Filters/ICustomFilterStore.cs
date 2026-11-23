namespace DevTools.ModelsContext.Filters;

public interface ICustomFilterStore
{
    IReadOnlyList<CustomFilter> Load();

    void Save(IReadOnlyList<CustomFilter> filters);
}
