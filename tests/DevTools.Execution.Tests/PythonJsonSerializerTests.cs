using System.Text.Json;
using DevTools.Execution.Providers.Python;
using Python.Runtime;
using ZLogger.Scintilla.Public;

namespace DevTools.Execution.Tests;

public sealed class PythonJsonSerializerUninitializedTests
{
    [Fact]
    public void WriteJson_WhenPythonNotInitialized_WritesTypeName()
    {
        if (PythonEngine.IsInitialized)
            Assert.Skip("Python is already initialized in this process.");

        var serializer = new PythonJsonSerializer();
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        serializer.WriteJson(writer, new object(), maxDepth: 4, maxItems: 50);
        writer.Flush();

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("System.Object", json, StringComparison.Ordinal);
    }
}

[Collection(nameof(PythonRuntimeCollection))]
public sealed class PythonJsonSerializerTests
{
    [Fact]
    public async Task CanSerialize_MatchesPyObjectAssignableTypes()
    {
        await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();

        var serializer = new PythonJsonSerializer();

        Assert.True(serializer.CanSerialize(typeof(PyObject)));
        Assert.True(serializer.CanSerialize(typeof(PyInt)));
        Assert.False(serializer.CanSerialize(typeof(string)));
        Assert.False(serializer.CanSerialize(typeof(int)));
    }

    [Fact]
    public async Task WriteJson_SerializesPythonDictToJson()
    {
        await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var serializer = new PythonJsonSerializer();
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        using (Py.GIL())
        {
            using var scope = Py.CreateScope();
            scope.Exec("value = {'name': 'widget', 'count': 3, 'tags': ['a', 'b']}");
            using var pyValue = scope.Get("value");

            serializer.WriteJson(writer, pyValue, maxDepth: 4, maxItems: 50);
        }

        writer.Flush();
        using var document = JsonDocument.Parse(stream.ToArray());
        Assert.Equal("widget", document.RootElement.GetProperty("name").GetString());
        Assert.Equal(3, document.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("tags").GetArrayLength());
    }

    [Fact]
    public async Task FormatText_ReturnsInvariantString_ForPythonValue()
    {
        await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var serializer = new PythonJsonSerializer();

        using (Py.GIL())
        {
            using var scope = Py.CreateScope();
            scope.Exec("value = 42");
            using var value = scope.Get("value");
            var text = serializer.FormatText(value);
            Assert.Equal("Python.Runtime.PyObject", text);
        }
    }

    [Fact]
    public void FormatText_Null_ReturnsNull()
    {
        var serializer = new PythonJsonSerializer();
        Assert.Null(serializer.FormatText(null));
    }

    [Fact]
    public async Task WriteJson_TruncatesDeepNestedStructures()
    {
        await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var serializer = new PythonJsonSerializer();
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        using (Py.GIL())
        {
            using var scope = Py.CreateScope();
            scope.Exec("""
                class Node:
                    def __init__(self, depth):
                        self.depth = depth
                        self.child = Node(depth - 1) if depth > 0 else None
                value = Node(8)
                """);
            using var pyValue = scope.Get("value");

            serializer.WriteJson(writer, pyValue, maxDepth: 2, maxItems: 10);
        }

        writer.Flush();
        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("Node", json, StringComparison.Ordinal);
    }
}
