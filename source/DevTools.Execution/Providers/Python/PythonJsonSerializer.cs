using System.Globalization;
using System.Text.Json;
using Python.Runtime;
using ZLogger.Scintilla.Public;

namespace DevTools.Execution.Providers.Python;

/// <summary>
/// <see cref="ICustomSerializer"/> that decomposes pythonnet <see cref="PyObject"/>
/// values into JSON using Python's native <c>json.dumps()</c> with a cached
/// serializer function — one cross-boundary call per object instead of
/// many individual PyObject method calls from .NET.
/// <para>
/// <see cref="CanSerialize"/> checks CLR type metadata only (no GIL required).
/// <see cref="WriteJson"/> and <see cref="FormatText"/> acquire the GIL internally
/// via <see cref="Py.GIL()"/>, which is reentrant if the caller already holds it.
/// </para>
/// </summary>
public sealed class PythonJsonSerializer : ICustomSerializer
{
    private PyObject? _serializeFn;
    private const string SerializeFunctionName = "_devtools_serialize";

    private static string BuildSerializeScript(int maxDepth, int maxItems) => $$"""
                                                                                import json as _json

                                                                                class _DevToolsEncoder(_json.JSONEncoder):
                                                                                    def __init__(self, *args, max_depth={{maxDepth}}, max_items={{maxItems}}, _depth=0, **kwargs):
                                                                                        super().__init__(*args, **kwargs)
                                                                                        self._max_depth = max_depth
                                                                                        self._max_items = max_items
                                                                                        self._depth = _depth

                                                                                    def default(self, o):
                                                                                        if self._depth >= self._max_depth:
                                                                                            return repr(o)

                                                                                        if hasattr(o, '__dict__'):
                                                                                            return self._write_dict(o.__dict__)

                                                                                        if hasattr(o, '__iter__') and not isinstance(o, (str, bytes)):
                                                                                            return self._write_items(o, self._max_items)

                                                                                        return repr(o)
                                                                                        
                                                                                    def _write_dict(self, o):
                                                                                        result = {'$type': type(o).__qualname__}
                                                                                        for k, v in o.items():
                                                                                            if not k.startswith('_'):
                                                                                                try:
                                                                                                    result[k] = v
                                                                                                except Exception:
                                                                                                    result[k] = repr(v)
                                                                                        return result
                                                                                    
                                                                                    def _write_items(self, o, max_items):
                                                                                        items = []
                                                                                        for i, item in enumerate(o):
                                                                                            if i >= max_items:
                                                                                                items.append(f'... ({max_items}+ items truncated)')
                                                                                                break
                                                                                            items.append(item)
                                                                                        return items

                                                                                def {{SerializeFunctionName}}(obj, max_depth={{maxDepth}}, max_items={{maxItems}}):
                                                                                    try:
                                                                                        return _json.dumps(obj, cls=_DevToolsEncoder, max_depth=max_depth,
                                                                                                           max_items=max_items, ensure_ascii=False,
                                                                                                           default=repr)
                                                                                    except Exception as e:
                                                                                        return _json.dumps({'$type': type(obj).__qualname__, '$error': str(e)})
                                                                                """;

    public bool CanSerialize(Type type)
    {
        return typeof(PyObject).IsAssignableFrom(type);
    }

    public void WriteJson(Utf8JsonWriter writer, object value, int maxDepth, int maxItems)
    {
        if (!PythonEngine.IsInitialized)
        {
            writer.WriteStringValue(value.GetType().FullName);
            return;
        }

        using (Py.GIL())
        {
            var jsonStr = CallSerialize(value, maxDepth, maxItems);
            if (jsonStr is null)
            {
                writer.WriteStringValue(value.GetType().FullName);
                return;
            }

            using var doc = JsonDocument.Parse(jsonStr);
            doc.RootElement.WriteTo(writer);
        }
    }

    public string? FormatText(object? value)
    {
        if (value is null) return null;

        if (!PythonEngine.IsInitialized)
            return value.GetType().FullName;

        try
        {
            using (Py.GIL())
                return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return value.GetType().FullName;
        }
    }

    private string? CallSerialize(object value, int maxDepth, int maxItems)
    {
        try
        {
            var fn = GetSerializeFunction(maxDepth, maxItems);
            using var pyVal = value as PyObject ?? value.ToPython();
            using var pyDepth = new PyInt(maxDepth);
            using var pyMaxItems = new PyInt(maxItems);
            using var result = fn.Invoke(pyVal, pyDepth, pyMaxItems);
            return result.ToString(CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private PyObject GetSerializeFunction(int maxDepth, int maxItems)
    {
        if (_serializeFn is not null)
            return _serializeFn;

        using var scope = Py.CreateScope();
        scope.Exec(BuildSerializeScript(maxDepth, maxItems));
        _serializeFn = scope.Get(SerializeFunctionName);
        return _serializeFn;
    }
}
