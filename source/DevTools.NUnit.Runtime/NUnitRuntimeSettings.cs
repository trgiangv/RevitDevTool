using NUnit;

namespace DevTools.NUnit.Runtime;

internal static class NUnitRuntimeSettings
{
    private const int WorkerCount = 1;

    public static Dictionary<string, object> Create(string workDirectory, bool runOnCallingThread = false)
    {
        var settings = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [FrameworkPackageSettings.NumberOfTestWorkers] = runOnCallingThread ? 0 : WorkerCount,
            [FrameworkPackageSettings.WorkDirectory] = workDirectory,
        };

        if (runOnCallingThread)
            settings[FrameworkPackageSettings.RunOnMainThread] = true;

        return settings;
    }
}
