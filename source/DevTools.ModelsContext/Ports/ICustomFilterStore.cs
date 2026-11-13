using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Ports;

public interface ICustomFilterStore
{
    IReadOnlyList<CustomFilter> Load();

    void Save(IReadOnlyList<CustomFilter> filters);
}
