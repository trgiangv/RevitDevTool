#if NET
namespace DevTools.NUnit.Host.Loading;

internal static class NUnitRuntimeUnloadVerifier
{
    private const int MaxVerificationCycles = 10;

    private const string UnloadedCode = "generation.unloaded";
    internal const string RetainedCode = "generation.retained";

    internal static DevTools.NUnit.Transport.Contracts.NUnitRuntimeDiagnostic Verify(WeakReference loadContextReference)
    {
        ArgumentNullException.ThrowIfNull(loadContextReference);

        return IsCollected(loadContextReference)
            ? new DevTools.NUnit.Transport.Contracts.NUnitRuntimeDiagnostic(
                UnloadedCode,
                "Generation ALC was collected after unload verification.")
            : new DevTools.NUnit.Transport.Contracts.NUnitRuntimeDiagnostic(
                RetainedCode,
                "Generation ALC retained after unload verification.");
    }

    private static bool IsCollected(WeakReference loadContextReference)
    {
        ArgumentNullException.ThrowIfNull(loadContextReference);

        for (var cycle = 0; cycle < MaxVerificationCycles; cycle++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

            if (!loadContextReference.IsAlive)
                return true;
        }

        return false;
    }
}
#endif
