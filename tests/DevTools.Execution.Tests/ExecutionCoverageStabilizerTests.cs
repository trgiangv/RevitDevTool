using DevTools.Execution;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.Backends;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.Hosting;
using DevTools.Mcp.Catalog;
using DevTools.Mcp.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace DevTools.Execution.Tests;

[Collection(nameof(NugetRestoreCollection))]
public sealed class ExecutionCoverageStabilizerTests
{
    [Fact]
    public void ExecutionGuardContext_SuppressMode_CanBeSetAndRead()
    {
        ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
        Assert.Equal(ExecutionGuardMode.Suppress, ExecutionGuardContext.Mode);
        ExecutionGuardContext.Mode = ExecutionGuardMode.Passthrough;
    }

    [Fact]
    public void PythonEmbedded_DoubleEnsureExtracted_IsIdempotent()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        PythonEmbedded.EnsureExtracted();
        PythonEmbedded.EnsureExtracted();
        Assert.False(string.IsNullOrWhiteSpace(PythonEmbedded.ResetScript));
    }

    [Fact]
    public async Task CSharpCompiler_CompileSimpleCommand_Succeeds()
    {
        const string code = """
            public sealed class ScriptCommand
            {
                public static int Value => 11;
            }
            """;

        var compiler = new CSharpCompiler(
            NullLogger<CSharpCompiler>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));

        var result = await compiler.CompileAsync(code, ExecutionTestHelpers.CreateScriptBridge(), ct: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.FormatDiagnostics());
        result.Cleanup?.Dispose();
    }

    [Fact]
    public void BuiltInMcpToolBackend_ReadResource_KnownTemplate_ReturnsPayload()
    {
        var backend = new BuiltInMcpToolBackend([], [new StubBuiltInResource("test://docs/{name}", "docs/{name}", "hello")]);
        var resource = new McpRegisteredResource
        {
            Id = "docs",
            TemplateDescriptor = new ResourceTemplate { UriTemplate = "test://docs/{name}", Name = "docs" },
            Binding = McpPrimitiveBinding.Create(ExecutionMode.CSharp, string.Empty, "docs", "read"),
        };

        var result = backend.ReadResource(resource, "test://docs/readme", TestContext.Current.CancellationToken);
        var text = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));
        Assert.Equal("hello", text.Text);
    }

    [Fact]
    public async Task PackageVersionChecker_AttachLatestVersions_CondaPackage_FetchesLatest()
    {
        var checker = new PackageVersionChecker(
            new NugetManager(NullLogger<NugetManager>.Instance),
            NullLogger<PackageVersionChecker>.Instance);

        var result = await checker.AttachLatestVersionsAsync(
            [new Package(Marketplace.CondaForge, "pip", "24.0", "24.0")],
            TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.False(string.IsNullOrWhiteSpace(result[0].LatestVersion));
    }

    private sealed class StubBuiltInResource(string uriTemplate, string protocolUri, string body) : IBuiltInMcpResource
    {
        public string UriTemplate => uriTemplate;

        public Resource ProtocolResource => new() { Uri = protocolUri, Name = "docs" };

        public ReadResourceResult Read(string uri) =>
            new()
            {
                Contents = [new TextResourceContents { Uri = uri, Text = body, MimeType = "text/plain" }],
            };
    }
}
