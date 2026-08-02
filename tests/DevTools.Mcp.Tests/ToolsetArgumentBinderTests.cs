using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Tests.Harness;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Tests;

/// <summary>ToolsetArgumentBinder augmented-parameter parity (T-ALC-02..05).</summary>
public sealed class ToolsetArgumentBinderTests
{
    [Fact]
    public void T_ALC_02_Invoke_BindsRequestContextIdentity()
    {
        DotnetToolsetMrtrStubs.ResetBindings();
        var method = typeof(DotnetToolsetMrtrStubs).GetMethod(nameof(DotnetToolsetMrtrStubs.BindCapture))!;
        var request = DotnetToolsetTestHarness.CreateRequest(
            arguments: DotnetToolsetTestHarness.Arguments(("name", "wall")));

        DotnetToolsetTestHarness.InvokeRaw(method, request);

        Assert.Same(request, DotnetToolsetMrtrStubs.LastContext);
    }

    [Fact]
    public void T_ALC_03_Invoke_BindsMcpServerFromRequest()
    {
        DotnetToolsetMrtrStubs.ResetBindings();
        var server = DotnetToolsetTestHarness.CreateMrtrServer(isMrtrSupported: true);
        var method = typeof(DotnetToolsetMrtrStubs).GetMethod(nameof(DotnetToolsetMrtrStubs.BindCapture))!;
        var request = DotnetToolsetTestHarness.CreateRequest(
            server,
            DotnetToolsetTestHarness.Arguments(("name", "wall")));

        DotnetToolsetTestHarness.InvokeRaw(method, request);

        Assert.Same(server.Object, DotnetToolsetMrtrStubs.LastServer);
        Assert.True(DotnetToolsetMrtrStubs.LastServer!.IsMrtrSupported);
    }

    [Fact]
    public void T_ALC_04_Invoke_BindsNopProgress_WhenProgressTokenAbsent()
    {
        DotnetToolsetMrtrStubs.ResetBindings();
        var method = typeof(DotnetToolsetMrtrStubs).GetMethod(nameof(DotnetToolsetMrtrStubs.BindCapture))!;
        var request = DotnetToolsetTestHarness.CreateRequest(
            arguments: DotnetToolsetTestHarness.Arguments(("name", "wall")));

        DotnetToolsetTestHarness.InvokeRaw(method, request);

        Assert.Equal("ToolsetNopProgress", DotnetToolsetMrtrStubs.LastProgress!.GetType().Name);
    }

    [Fact]
    public void T_ALC_04_Invoke_BindsProgressReporter_WhenProgressTokenPresent()
    {
        DotnetToolsetMrtrStubs.ResetBindings();
        var method = typeof(DotnetToolsetMrtrStubs).GetMethod(nameof(DotnetToolsetMrtrStubs.BindCapture))!;
        var request = DotnetToolsetTestHarness.CreateRequest(
            arguments: DotnetToolsetTestHarness.Arguments(("name", "wall")),
            progressToken: new ProgressToken("progress-1"));

        DotnetToolsetTestHarness.InvokeRaw(method, request);

        Assert.Equal("ToolsetProgressReporter", DotnetToolsetMrtrStubs.LastProgress!.GetType().Name);
    }

    [Fact]
    public void T_ALC_04_Invoke_BindsClaimsPrincipalFromRequest()
    {
        DotnetToolsetMrtrStubs.ResetBindings();
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
        var method = typeof(DotnetToolsetMrtrStubs).GetMethod(nameof(DotnetToolsetMrtrStubs.BindUser))!;
        var request = DotnetToolsetTestHarness.CreateRequest(user: principal);

        var result = Assert.IsType<string>(DotnetToolsetTestHarness.InvokeRaw(method, request));

        Assert.Equal("has-user", result);
        Assert.Same(principal, DotnetToolsetMrtrStubs.LastUser);
    }

    [Fact]
    public void T_ALC_05_Invoke_BindsOrdinaryArgumentsFromParams()
    {
        DotnetToolsetMrtrStubs.ResetBindings();
        var method = typeof(DotnetToolsetMrtrStubs).GetMethod(nameof(DotnetToolsetMrtrStubs.BindCapture))!;
        var request = DotnetToolsetTestHarness.CreateRequest(
            arguments: DotnetToolsetTestHarness.Arguments(("name", "beam"), ("flag", true)));

        DotnetToolsetTestHarness.InvokeRaw(method, request);

        Assert.Equal("beam", DotnetToolsetMrtrStubs.LastName);
        Assert.True(DotnetToolsetMrtrStubs.LastFlag);
    }

    [Fact]
    public void T_ALC_05_Invoke_BindsDiRegisteredService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<FactoryDiProbe>();
        var provider = services.BuildServiceProvider();
        var method = typeof(FactoryDiProbe).GetMethod(nameof(FactoryDiProbe.WithService))!;
        var request = DotnetToolsetTestHarness.CreateRequest(
            arguments: DotnetToolsetTestHarness.Arguments(("label", "x")));

        var result = Assert.IsType<string>(DotnetToolsetTestHarness.InvokeRaw(method, request, provider));

        Assert.EndsWith(":x", result, StringComparison.Ordinal);
    }

    private sealed class FactoryDiProbe
    {
        public static string WithService(FactoryDiProbe probe, string label) => $"{probe.GetHashCode()}:{label}";
    }
}
