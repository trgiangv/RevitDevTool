using DevTools.Execution.Providers.Python;

using DevTools.Utilities;

using Microsoft.Extensions.Logging.Abstractions;



namespace DevTools.Execution.Tests;



public sealed class UvEnvironmentProviderTests

{

    [Fact]

    public void UvEnvRoot_IsUnderApplicationData()

    {

        var expected = Path.Combine(AppUtils.GetApplicationDataPath(), "uv-env");

        Assert.Equal(expected, UvEnvironmentProvider.UvEnvRoot);
        Assert.Equal(Path.Combine(expected, "uv-python"), UvEnvironmentProvider.UvPythonInstallDir);
        Assert.Equal(Path.Combine(expected, "uv-cache"), UvEnvironmentProvider.UvCacheDir);

    }



    [Fact]

    public void BoundEnvDir_KeysToProbedHostVersion()

    {

        var provider = new UvEnvironmentProvider(NullLogger<UvEnvironmentProvider>.Instance, () => "3.13");



        Assert.Equal("3.13", provider.BoundPythonVersion);

        Assert.Equal(

            Path.Combine(UvEnvironmentProvider.UvEnvRoot, "3.13"),

            provider.BoundEnvDir);

        Assert.Equal(

            Path.Combine(provider.BoundEnvDir, "Scripts", "python.exe"),

            provider.PythonExe);
    }



    [Fact]

    public void BoundPythonVersion_ProbesHostOnlyOnce()

    {

        var probeCount = 0;

        var provider = new UvEnvironmentProvider(

            NullLogger<UvEnvironmentProvider>.Instance,

            () =>

            {

                probeCount++;

                return "3.13";

            });



        _ = provider.BoundPythonVersion;

        _ = provider.BoundPythonVersion;

        _ = provider.BoundPythonVersion;



        Assert.Equal(1, probeCount);

    }



    [Fact]

    public async Task SetupEnvironmentAsync_WithoutHostInterpreter_Throws()

    {

        var provider = new UvEnvironmentProvider(NullLogger<UvEnvironmentProvider>.Instance, () => null);

        await Assert.ThrowsAsync<InvalidOperationException>(provider.SetupEnvironmentAsync);

    }



    [Fact]

    public void PythonExe_EmptyWhenNoHostInterpreter()

    {

        var provider = new UvEnvironmentProvider(NullLogger<UvEnvironmentProvider>.Instance, () => null);

        Assert.Equal(string.Empty, provider.PythonExe);
        Assert.Equal(string.Empty, provider.BoundEnvDir);
        Assert.Equal(string.Empty, provider.SitePackagesDir);
        Assert.False(provider.IsEnvironmentReady());

    }



    [Theory]

    [InlineData("3")]

    [InlineData("3.13.2")]

    public void BoundPythonVersion_NullWhenProbeReturnsMalformedVersion(string malformed)

    {

        var provider = new UvEnvironmentProvider(

            NullLogger<UvEnvironmentProvider>.Instance,

            () => malformed);



        Assert.Null(provider.BoundPythonVersion);

        Assert.Equal(string.Empty, provider.BoundEnvDir);

    }

}

