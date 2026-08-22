namespace DevTools.TestAdapter;

/// <summary>
/// Registers Autodesk API resolve paths from discovery-refs.txt for test executables.
/// </summary>
public static class DiscoveryRefsRegistration
{
    public static void RegisterForExecutingAssembly()
    {
        var location = System.Reflection.Assembly.GetExecutingAssembly().Location;
        if (location.Length > 0)
            RegisterForAssembly(location);
    }

    public static void RegisterForAssembly(string assemblyPath) =>
        RuntimeAssemblyResolver.EnsureRegistered(assemblyPath);
}
