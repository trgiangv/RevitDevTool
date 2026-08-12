using System.Reflection;

namespace DevTools.NUnit.Runtime.Tests;

internal static class DedicatedTestFixturesHarness
{
    public const string GenerationId = "dedicated-fixtures";

    public static string AssemblyPath =>
        Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "DevTools.NUnit.Runtime.Tests.Fixtures.dll"));

    public static NUnitRuntimeSession CreateSession() =>
        new(Assembly.LoadFrom(AssemblyPath), AssemblyPath, GenerationId);

    public static void ResetBlockingState() =>
        Interlocked.Exchange(ref Fixtures.BlockingRunState.Entered, 0);

    public static void ResetCancellationProbeState() =>
        Interlocked.Exchange(ref Fixtures.CancellationProbeState.BodyEntered, 0);

    public static void ResetPartialCancelState()
    {
        Interlocked.Exchange(ref Fixtures.PartialCancelState.FirstCompleted, 0);
        Interlocked.Exchange(ref Fixtures.PartialCancelState.SecondEntered, 0);
    }

    public const string BlockingTestFullName =
        "DevTools.NUnit.Runtime.Tests.Fixtures.BlockingFixture.Blocks_UntilRunStopped";

    public const string BlockingFilter =
        "<filter><test>DevTools.NUnit.Runtime.Tests.Fixtures.BlockingFixture.Blocks_UntilRunStopped</test></filter>";

    public const string AttachmentTestFullName =
        "DevTools.NUnit.Runtime.Tests.Fixtures.AttachmentFixture.CreatesAttachmentAndWarning";

    public const string AttachmentFilter =
        "<filter><test>DevTools.NUnit.Runtime.Tests.Fixtures.AttachmentFixture.CreatesAttachmentAndWarning</test></filter>";

    public const string CancellationProbeFilter =
        "<filter><test>DevTools.NUnit.Runtime.Tests.Fixtures.CancellationProbeFixture.BodyMustNotRunWhenRunIsCancelled</test></filter>";

    public const string PartialCancelFilter =
        "<filter><test>DevTools.NUnit.Runtime.Tests.Fixtures.PartialCancelFixture</test></filter>";

    public const string DuplicateNameFilter =
        "<filter><test>DevTools.NUnit.Runtime.Tests.Fixtures.DuplicateNameFixture</test></filter>";
}
