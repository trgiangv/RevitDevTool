using DevTools.Testing.Abstractions;
using Microsoft.Testing.Platform.Builder;

namespace DevTools.NUnit.MTP;

/// <summary>
/// Microsoft.Testing.Platform builder hook, compiled into the consumer testhost.
/// The generated entry point requires
/// <c>AddExtensions(ITestApplicationBuilder, string[])</c>. The arguments
/// array is unused: this hook only assigns <see cref="TestingDiscovery"/>.
/// Source lives in the adapter package, not DevTools.NUnit.MTP.dll, so the
/// sibling does not reference Microsoft.Testing.Platform.
/// </summary>
public static class NUnitMtpBuilderHook
{
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] arguments)
    {
        if (testApplicationBuilder is null)
            throw new ArgumentNullException(nameof(testApplicationBuilder));
        _ = arguments;
        var discoverer = new NUnitTestDiscoverer();
        TestingDiscovery.Register(discoverer, new NUnitTestRunMapper());
    }
}
