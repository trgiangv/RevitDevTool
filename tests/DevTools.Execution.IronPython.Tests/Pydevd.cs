using DevTools.Execution.Providers.IronPython;

namespace DevTools.Execution.Tests;

internal static class Pydevd
{
    private static readonly Lazy<Task<string?>> Install = new(async () =>
    {
        try
        {
            await PydevdInstaller.EnsureInstalledAsync();
            return null;
        }
        catch (Exception ex)
        {
            return $"pydevd download failed: {ex.Message}";
        }
    });

    public static void EnsureInstalled()
    {
        if (Install.Value.GetAwaiter().GetResult() is { } reason)
            Assert.Inconclusive(reason);
    }
}
