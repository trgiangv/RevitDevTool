using DevTools.Execution.Abstractions;
using DevTools.Execution.Tests.AssemblyIsolation;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Dotnet;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevTools.Execution.Tests;

public sealed class AssemblyExecutionProviderTests
{
    [Fact]
    public async Task DiscoverAsync_BuildsNamespaceTreeForCommands()
    {
        using var graph = DynamicCommandGraph.Create("assembly-provider");
        var discovery = new Mock<ICommandDiscovery>();
        discovery
            .Setup(d => d.ParseCommands(graph.EntryPath))
            .Returns([
                new CommandItem(graph.EntryPath, "Fixture.Entry") { Name = "Entry" },
            ]);

        var provider = new AssemblyExecutionProvider(
            discovery.Object,
            ExecutionTestHelpers.InlineHostContext(),
            Mock.Of<ICommandRunner>(),
            NullLogger<AssemblyExecutionProvider>.Instance);

        var nodes = await provider.DiscoverAsync(graph.EntryPath, TestContext.Current.CancellationToken);
        var root = Assert.IsType<ExecutionNodeRoot>(Assert.Single(nodes));
        var namespaceNode = Assert.IsType<ExecutionNodeIntermediate>(Assert.Single(root.Children));
        var command = Assert.IsType<ExecutionNode>(Assert.Single(namespaceNode.Children));

        Assert.Equal(ExecutionMode.Dotnet, command.ExecutionMode);
        Assert.Equal("Entry", command.Name);
    }

    [Fact]
    public async Task DiscoverAsync_InvalidPath_ReturnsEmpty()
    {
        var provider = new AssemblyExecutionProvider(
            Mock.Of<ICommandDiscovery>(),
            ExecutionTestHelpers.InlineHostContext(),
            Mock.Of<ICommandRunner>(),
            NullLogger<AssemblyExecutionProvider>.Instance);

        var nodes = await provider.DiscoverAsync("missing.dll", TestContext.Current.CancellationToken);

        Assert.Empty(nodes);
    }

    [Fact]
    public void CanHandle_AcceptsDllFilesOnly()
    {
        var provider = new AssemblyExecutionProvider(
            Mock.Of<ICommandDiscovery>(),
            ExecutionTestHelpers.InlineHostContext(),
            Mock.Of<ICommandRunner>(),
            NullLogger<AssemblyExecutionProvider>.Instance);

        using var graph = DynamicCommandGraph.Create("assembly-handle");
        Assert.True(provider.CanHandle(graph.EntryPath));
        Assert.False(provider.CanHandle(graph.Directory));
    }
}
