using DevTools.Testing.Abstractions.Runtime;
using Microsoft.Testing.Platform.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevTools.MSTest.Runtime;

internal static class MSTestPlatformPolicy
{
    private const string CancellationSourceTypeName =
        "Microsoft.Testing.Platform.Services.ITestApplicationCancellationTokenSource";

    private static readonly Version ExpectedFrameworkVersion = new(4, 4, 1, 0);
    private static readonly Version ExpectedPlatformVersion = new(2, 4, 1, 0);

    public static void EnsurePinnedVersions()
    {
        var framework = typeof(TestClassAttribute).Assembly;
        var platform = typeof(TestApplication).Assembly;
        var frameworkVersion = framework.GetName().Version;
        var platformVersion = platform.GetName().Version;
        if (frameworkVersion == ExpectedFrameworkVersion
            && platformVersion == ExpectedPlatformVersion)
        {
            return;
        }

        throw new InvalidOperationException(
            "MSTest runtime requires loaded MSTest.TestFramework 4.4.1 and Microsoft.Testing.Platform 2.4.1. Found "
            + framework.GetName().Name + " " + frameworkVersion + " and "
            + platform.GetName().Name + " " + platformVersion + ".");
    }

    /// <summary>
    /// Stops MTP from starting the next test (and signals <c>TestContext.CancellationToken</c>).
    /// A test body already running, such as a Revit API call, is never interrupted.
    /// The cancel source is internal to MTP; <see cref="EnsurePinnedVersions"/> guards this
    /// reflection and <see cref="InternalMembers"/> caches it.
    /// </summary>
    public static void Cancel(ITestApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var sourceType = InternalMembers.Type(typeof(TestApplication).Assembly, CancellationSourceTypeName);
        var provider = InternalMembers.Property(application.GetType(), "ServiceProvider").GetValue(application)
            as IServiceProvider
            ?? throw new InvalidOperationException("TestApplication.ServiceProvider is unavailable.");
        var source = provider.GetService(sourceType)
            ?? throw new InvalidOperationException(CancellationSourceTypeName + " is not registered.");
        InternalMembers.Method(sourceType, "Cancel").Invoke(source, null);
    }
}
