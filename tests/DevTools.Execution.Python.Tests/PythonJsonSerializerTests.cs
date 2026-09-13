using System.Text.Json;
using DevTools.Execution.Providers.Python;
using Python.Runtime;
using ZLogger.Scintilla.Public;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PythonJsonSerializerUninitializedTests
{
    [TestMethod]
    public void WriteJson_WhenPythonNotInitialized_WritesTypeName()
    {
        if (PythonEngine.IsInitialized)
            Assert.Inconclusive("Python is already initialized in this process.");

        var serializer = new PythonJsonSerializer();
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        serializer.WriteJson(writer, new object(), maxDepth: 4, maxItems: 50);
        writer.Flush();

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("System.Object", json, StringComparison.Ordinal);
    }
}

[TestClass]
public sealed class PythonJsonSerializerTests
{
    [TestMethod]
    public async Task CanSerialize_MatchesPyObjectAssignableTypes()
    {
        await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();

        var serializer = new PythonJsonSerializer();

        Assert.IsTrue(serializer.CanSerialize(typeof(PyObject)));
        Assert.IsTrue(serializer.CanSerialize(typeof(PyInt)));
        Assert.IsFalse(serializer.CanSerialize(typeof(string)));
        Assert.IsFalse(serializer.CanSerialize(typeof(int)));
    }

    [TestMethod]
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
        Assert.AreEqual("widget", document.RootElement.GetProperty("name").GetString());
        Assert.AreEqual(3, document.RootElement.GetProperty("count").GetInt32());
        Assert.AreEqual(2, document.RootElement.GetProperty("tags").GetArrayLength());
    }

    [TestMethod]
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
            Assert.AreEqual("Python.Runtime.PyObject", text);
        }
    }

    [TestMethod]
    public void FormatText_Null_ReturnsNull()
    {
        var serializer = new PythonJsonSerializer();
        Assert.IsNull(serializer.FormatText(null));
    }

    [TestMethod]
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
