using DevTools.ModelsContext.Filters;
using DevTools.Settings;

namespace RevitDevTool.Tools.ModelsContext.Filters;

public sealed class CustomFilterStore(IFileConfig<PathOptions> fileConfig) : ICustomFilterStore
{
    public IReadOnlyList<CustomFilter> Load()
    {
        try
        {
            return StoredFilters.Read(fileConfig.Load<ModelContextFilters>());
        }
        catch (Exception exception)
        {
            throw Failed(exception);
        }
    }

    public void Save(IReadOnlyList<CustomFilter> filters)
    {
        try
        {
            fileConfig.Save(StoredFilters.Write(filters));
        }
        catch (Exception exception)
        {
            throw Failed(exception);
        }
    }

    private InvalidOperationException Failed(Exception exception)
    {
        return new InvalidOperationException(
            $"{exception.Message} ({fileConfig.Options.GetSettingsPath<ModelContextFilters>()})", exception);
    }
}
