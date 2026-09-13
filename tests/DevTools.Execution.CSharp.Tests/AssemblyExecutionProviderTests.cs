using DevTools.Execution.Abstractions;
using DevTools.Execution.Tests.AssemblyIsolation;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Dotnet;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class AssemblyExecutionProviderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
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

        var nodes = (await provider.DiscoverAsync(graph.EntryPath, TestContext.CancellationToken)).ToList();
        Assert.AreEqual(1, nodes.Count);
        var root = Assert.IsInstanceOfType<ExecutionNodeRoot>(nodes[0]);
        var rootChildren = root.Children.ToList();
        Assert.AreEqual(1, rootChildren.Count);
        var namespaceNode = Assert.IsInstanceOfType<ExecutionNodeIntermediate>(rootChildren[0]);
        var namespaceChildren = namespaceNode.Children.ToList();
        Assert.AreEqual(1, namespaceChildren.Count);
        var command = Assert.IsInstanceOfType<ExecutionNode>(namespaceChildren[0]);

        Assert.AreEqual(ExecutionMode.Dotnet, command.ExecutionMode);
        Assert.AreEqual("Entry", command.Name);
    }

    [TestMethod]
    public async Task DiscoverAsync_InvalidPath_ReturnsEmpty()
    {
        var provider = new AssemblyExecutionProvider(
            Mock.Of<ICommandDiscovery>(),
            ExecutionTestHelpers.InlineHostContext(),
            Mock.Of<ICommandRunner>(),
            NullLogger<AssemblyExecutionProvider>.Instance);

        var nodes = await provider.DiscoverAsync("missing.dll", TestContext.CancellationToken);

        Assert.IsEmpty(nodes);
    }

    [TestMethod]
    public void CanHandle_AcceptsDllFilesOnly()
    {
        var provider = new AssemblyExecutionProvider(
            Mock.Of<ICommandDiscovery>(),
            ExecutionTestHelpers.InlineHostContext(),
            Mock.Of<ICommandRunner>(),
            NullLogger<AssemblyExecutionProvider>.Instance);

        using var graph = DynamicCommandGraph.Create("assembly-handle");
        Assert.IsTrue(provider.CanHandle(graph.EntryPath));
        Assert.IsFalse(provider.CanHandle(graph.Directory));
    }
}
