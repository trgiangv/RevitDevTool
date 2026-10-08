using System.Text.Json;
using DevTools.Daemon.Mcp.Code;
using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Tests;

[TestClass]
public sealed class CodeModeResultStoreTests
{
    [TestMethod]
    public void ShortText_IsUnchangedAndWritesNoFile()
    {
        using var sandbox = new Sandbox();

        var result = CodeModeResult.ToCallToolResult(new { category = "Ducts", count = 2 }, sandbox.Store);

        Assert.IsTrue(result.IsError is not true);
        Assert.IsEmpty(sandbox.Files());
    }

    [TestMethod]
    public void LongText_ReturnsTheStartTheEndAndThePath()
    {
        using var sandbox = new Sandbox();
        var full = new string('a', 10) + new string('b', CodeModeResultStore.ViewChars) + new string('c', 10);

        var result = CodeModeResult.ToCallToolResult(full, sandbox.Store);

        Assert.IsTrue(result.IsError is not true);
        Assert.IsNull(result.StructuredContent);
        var view = ((TextContentBlock)result.Content[0]).Text;
        Assert.IsTrue(view.StartsWith(full[..(CodeModeResultStore.ViewChars / 2)], StringComparison.Ordinal));
        Assert.IsTrue(view.Contains(full[^10..], StringComparison.Ordinal));
        var path = PathIn(view);
        Assert.AreEqual(full, File.ReadAllText(path));
        Assert.HasCount(1, sandbox.Files());
    }

    [TestMethod]
    public void StructuredContent_IsPartOfTheSavedText()
    {
        using var sandbox = new Sandbox();
        var payload = new string('e', CodeModeResultStore.ViewChars);
        var source = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "Found 1 elements" }],
            StructuredContent = JsonSerializer.SerializeToElement(new { count = 1, elements = payload }),
        };

        var result = CodeModeResult.ToCallToolResult(source, sandbox.Store);

        Assert.IsTrue(result.IsError is not true);
        Assert.IsNull(result.StructuredContent);
        var view = ((TextContentBlock)result.Content[0]).Text;
        var path = PathIn(view);
        var saved = File.ReadAllText(path);
        Assert.IsTrue(saved.StartsWith("Found 1 elements\n", StringComparison.Ordinal));
        Assert.IsTrue(saved.Contains(payload, StringComparison.Ordinal));
        Assert.IsTrue(view.Contains(path, StringComparison.Ordinal));
    }

    [TestMethod]
    public void TextOverOneMebibyte_IsSavedInsteadOfDropped()
    {
        using var sandbox = new Sandbox();
        var full = new string('q', CodeModeResult.MaxBytes + 1);

        var result = CodeModeResult.ToCallToolResult(full, sandbox.Store);

        Assert.IsTrue(result.IsError is not true);
        var path = PathIn(((TextContentBlock)result.Content[0]).Text);
        Assert.AreEqual(full.Length, File.ReadAllText(path).Length);
    }

    [TestMethod]
    public void FileCap_DeletesThePartialAndReturnsNoHandle()
    {
        using var sandbox = new Sandbox(maxFileBytes: 200, maxProcessBytes: 10_000_000);

        var result = CodeModeResult.ToCallToolResult(new string('z', CodeModeResultStore.ViewChars + 1), sandbox.Store);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("Saved result exceeds 64 MB.", ((TextContentBlock)result.Content[0]).Text);
        Assert.IsEmpty(sandbox.Files());
    }

    [TestMethod]
    public void ProcessCap_DeletesThePartialAndReturnsNoHandle()
    {
        using var sandbox = new Sandbox(maxFileBytes: 10_000_000, maxProcessBytes: 50_000);
        Assert.IsTrue(CodeModeResult.ToCallToolResult(new string('a', CodeModeResultStore.ViewChars + 1), sandbox.Store).IsError is not true);

        var result = CodeModeResult.ToCallToolResult(new string('b', CodeModeResultStore.ViewChars + 1), sandbox.Store);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("Saved result exceeds 256 MB.", ((TextContentBlock)result.Content[0]).Text);
        Assert.HasCount(1, sandbox.Files());
    }

    private static string PathIn(string view)
    {
        const string marker = "[Full output: ";
        var start = view.LastIndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0);
        return view[(start + marker.Length)..].TrimEnd(']', '\r', '\n');
    }

    private sealed class Sandbox : IDisposable
    {
        public Sandbox(long maxFileBytes = CodeModeResultStore.MaxFileBytes, long maxProcessBytes = CodeModeResultStore.MaxProcessBytes)
        {
            Folder = Path.Combine(Path.GetTempPath(), "rdt-art-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Folder);
            Store = new CodeModeResultStore(Folder, Environment.ProcessId, maxFileBytes, maxProcessBytes);
        }

        public string Folder { get; }

        public CodeModeResultStore Store { get; }

        public string[] Files() =>
            System.IO.Directory.Exists(Folder)
                ? System.IO.Directory.GetFiles(Folder, "RevitDevTool-*-*.txt")
                : [];

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Folder))
                System.IO.Directory.Delete(Folder, recursive: true);
        }
    }
}
