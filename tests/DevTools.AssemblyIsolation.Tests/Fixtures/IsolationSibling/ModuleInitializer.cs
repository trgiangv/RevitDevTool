using System.Runtime.CompilerServices;

namespace IsolationSibling;

public static class Initializer
{
    [ModuleInitializer]
    public static void Run()
    {
        var markerPath = Environment.GetEnvironmentVariable("DEVTOOLS_ISOLATION_SIBLING_MARKER");
        if (!string.IsNullOrWhiteSpace(markerPath))
            File.WriteAllText(markerPath, "loaded");
    }
}
