namespace DevTools.Mcp.Routing.Catalog;

public static class DynamicToolCatalogView
{
    public static object Build(this DynamicToolCatalog catalog)
    {
        var registrations = catalog.List();
        var tools = registrations
            .GroupBy(item => item.Tool.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                name = group.Key,
                registrations = group.Select(item => new
                {
                    hostInstanceId = item.Instance.ProcessId,
                    hostApp = item.Instance.HostApp,
                    versionNumber = item.Instance.VersionNumber,
                    pipeName = item.PipeName,
                    description = item.Tool.Description,
                    inputSchema = item.Tool.InputSchema
                }).ToArray()
            })
            .ToArray();

        return new
        {
            tools,
            toolCount = tools.Length,
            registrationCount = registrations.Count
        };
    }
}
