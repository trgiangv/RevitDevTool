using NUnit;

namespace DevTools.NUnit.Runtime;

internal static class NUnitRuntimeSettings
{
    public const int WorkerCount = 1;

    public static Dictionary<string, object> Create(string workDirectory) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            [FrameworkPackageSettings.NumberOfTestWorkers] = WorkerCount,
            [FrameworkPackageSettings.WorkDirectory] = workDirectory,
        };
}
