namespace DevTools.Mcp.Routing.Catalog;

public static class DynamicToolCatalogView
{
    public static DynamicCatalogSummary Build(this DynamicToolCatalog catalog)
    {
        var registrations = catalog.List();
        var tools = registrations
            .GroupBy(item => item.Tool.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DynamicToolSummary(
                group.Key,
                group.Select(item => new DynamicToolRegistration(
                    item.Instance.ProcessId,
                    item.Instance.HostApp,
                    item.Instance.VersionNumber,
                    item.PipeName,
                    item.Tool.Description,
                    item.Tool.InputSchema)).ToArray()))
            .ToArray();

        return new DynamicCatalogSummary(tools, tools.Length, registrations.Count);
    }
}
