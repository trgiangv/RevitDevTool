using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Internal;
using ZLogger;
namespace RevitDevTool.Scintilla.Logger;

public sealed class ScintillaLogProcessor : IAsyncLogProcessor
{
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? _bufferWriter;

    private readonly ZLoggerOptions _options;
    private readonly IZLoggerFormatter _formatter;
    private readonly ILogEntrySink _ingress;

    internal ScintillaLogProcessor(ZLoggerOptions options, ILogEntrySink ingress)
    {

        _options = options ?? throw new ArgumentNullException(nameof(options));
        _formatter = options.CreateFormatter();
        _ingress = ingress ?? throw new ArgumentNullException(nameof(ingress));
    }

    public void Post(IZLoggerEntry log)
    {
        try
        {
            var bufferWriter = GetThreadBufferWriter();
            bufferWriter.Clear();
            log.FormatUtf8(bufferWriter, _formatter);

            var writtenMemory = bufferWriter.WrittenMemory;
            var messageLength = writtenMemory.Length;

            // OPTIMIZATION: Copy only once from bufferWriter's span
            byte[] rented;
            if (messageLength > 0)
            {
                rented = RentArray(messageLength);
                writtenMemory.Span.CopyTo(rented);
            }
            else
            {
                rented = Array.Empty<byte>();
                messageLength = 0;
            }

            // Extract structured payload and build properties
            object? structuredPayload = null;
            IReadOnlyList<string> payloadTypeNames = Array.Empty<string>();
            if (log.ParameterCount > 0 || log.LogInfo.Context is { })
            {
                structuredPayload = TryExtractStructuredPayloadObject(log, out payloadTypeNames);
            }
            var properties = BuildProperties(log, structuredPayload, payloadTypeNames);

            var logEntry = new LogEntry
            {
                // Use the timestamp captured by ZLogger at call-site, not the time we process it.
                TimestampUtc = log.LogInfo.Timestamp.Utc.UtcDateTime,
                Level = log.LogInfo.LogLevel,
                // Category.Name is a pre-computed string inside ZLogger — no allocation here.
                Source = log.LogInfo.Category.Name,
                Message = new ArraySegment<byte>(rented, 0, messageLength),
                // Exception.ToString() allocates once; only non-null when an exception was logged.
                ExceptionText = log.LogInfo.Exception?.ToString(),
                Properties = properties
            };

            logEntry.AttachBufferLease(ReleaseArray);

            if (!_ingress.TryPost(logEntry))
                logEntry.ReleaseBuffer();
        }
        catch (Exception ex)
        {
            _options.InternalErrorLogger?.Invoke(ex);
        }
        finally
        {
            log.Return();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ArrayBufferWriter<byte> GetThreadBufferWriter()
    {
        if (_bufferWriter is null)
            _bufferWriter = new ArrayBufferWriter<byte>(1024);

        return _bufferWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] RentArray(int minimumLength)
    {
        if (minimumLength <= 0)
            return Array.Empty<byte>();

        return ArrayPool<byte>.Shared.Rent(minimumLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReleaseArray(byte[] buffer)
    {
        if (buffer.Length == 0)
            return;

        ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return default;
    }

    private static IReadOnlyDictionary<string, object?> BuildProperties(
        IZLoggerEntry entry,
        object? structuredPayload,
        IReadOnlyList<string> payloadTypeNames)
    {
        Dictionary<string, object?>? properties = null;

        var scopeState = entry.LogInfo.ScopeState;
        if (scopeState is { Properties.IsEmpty: false })
        {
            var scopeProperties = scopeState.Properties;
            properties = new Dictionary<string, object?>(scopeProperties.Length, StringComparer.Ordinal);
            for (var i = 0; i < scopeProperties.Length; i++)
            {
                var item = scopeProperties[i];
                if (!string.IsNullOrWhiteSpace(item.Key))
                    properties[item.Key] = item.Value;
            }
        }

        if (structuredPayload is not null)
        {
            properties ??= new Dictionary<string, object?>(StringComparer.Ordinal);
            properties[LogPropertyKeys.StructuredPayloadObject] = structuredPayload;
        }

        if (payloadTypeNames.Count > 0)
        {
            properties ??= new Dictionary<string, object?>(StringComparer.Ordinal);
            if (payloadTypeNames.Count == 1)
                properties[LogPropertyKeys.StructuredPayloadTypeName] = payloadTypeNames[0];
            else
                properties[LogPropertyKeys.StructuredPayloadTypeNames] = payloadTypeNames.ToArray();
        }

        return properties is null || properties.Count == 0
            ? LogEntry.EmptyProperties
            : properties;
    }

    private static object? TryExtractStructuredPayloadObject(IZLoggerEntry entry, out IReadOnlyList<string> payloadTypeNames)
    {
        object? firstCandidate = null;
        List<object>? candidateList = null;
        string? firstTypeName = null;
        List<string>? typeNameList = null;

        static void AddCandidate(ref object? first, ref List<object>? list, object candidate)
        {
            if (first is null)
            {
                first = candidate;
                return;
            }

            list ??= new List<object>(4) { first };
            list.Add(candidate);
        }

        static void AddTypeName(ref string? first, ref List<string>? list, string typeName)
        {
            if (first is null)
            {
                first = typeName;
                return;
            }

            list ??= new List<string>(4) { first };
            list.Add(typeName);
        }

        // Use ZLogger's native parameter API — no reflection needed for parameters.
        for (var i = 0; i < entry.ParameterCount; i++)
        {
            var raw = entry.GetParameterValue(i);
            if (!IsStructuredPayloadCandidate(raw))
                continue;

            if (raw is IDictionary || raw is IEnumerable && raw is not string)
            {
                AddCandidate(ref firstCandidate, ref candidateList, raw!);
            }
            else
            {
                AddCandidate(ref firstCandidate, ref candidateList, raw!);
            }

            var fullName = raw!.GetType().FullName;
            if (!string.IsNullOrWhiteSpace(fullName))
                AddTypeName(ref firstTypeName, ref typeNameList, fullName!);
        }

        // Context objects are arbitrary POCOs — reflection is still needed here.
        if (firstCandidate is null && IsStructuredPayloadCandidate(entry.LogInfo.Context))
        {
            var contextPayload = entry.LogInfo.Context!;
            AddCandidate(ref firstCandidate, ref candidateList, NormalizeContextPayload(contextPayload));
            var fullName = contextPayload.GetType().FullName;
            if (!string.IsNullOrWhiteSpace(fullName))
                AddTypeName(ref firstTypeName, ref typeNameList, fullName!);
        }

        if (typeNameList is not null)
            payloadTypeNames = typeNameList;
        else if (firstTypeName is not null)
            payloadTypeNames = new[] { firstTypeName };
        else
            payloadTypeNames = Array.Empty<string>();

        if (firstCandidate is null)
            return null;
        if (candidateList is null)
            return firstCandidate;
        return candidateList;
    }

    // Caches compiled Func<object, object?> getters per type for Context POCOs only.
    private static readonly ConcurrentDictionary<Type, (string Name, Func<object, object?> Getter)[]>
        ContextAccessorCache = new();

    /// <summary>
    /// Reflection-based normalization for <see cref="IZLoggerEntry.LogInfo"/> Context POCOs.
    /// This is the only path that uses reflection — parameter values use ZLogger's native API.
    /// </summary>
    private static object NormalizeContextPayload(object value)
    {
        if (value is IDictionary || value is IEnumerable && value is not string)
            return value;

        var type = value.GetType();
        var accessors = ContextAccessorCache.GetOrAdd(type, static t =>
        {
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(static p => p.CanRead && p.GetIndexParameters().Length == 0)
                .ToArray();

            var result = new (string Name, Func<object, object?> Getter)[props.Length];
            for (var i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                var param = Expression.Parameter(typeof(object), "obj");
                var cast = Expression.Convert(param, t);
                var body = Expression.Convert(Expression.Property(cast, prop), typeof(object));
                result[i] = (prop.Name, Expression.Lambda<Func<object, object?>>(body, param).Compile());
            }
            return result;
        });

        if (accessors.Length == 0)
            return value;

        var normalized = new Dictionary<string, object?>(accessors.Length, StringComparer.Ordinal);
        for (var i = 0; i < accessors.Length; i++)
        {
            var (name, getter) = accessors[i];
            try
            {
                normalized[name] = getter(value);
            }
            catch
            {
                // Skip properties that throw
            }
        }

        return normalized.Count == 0 ? value : normalized;
    }

    private static bool IsStructuredPayloadCandidate(object? value)
    {
        if (value is null || value is string || value is Exception)
            return false;

        if (value is byte[] || value is ArraySegment<byte> || value is ReadOnlyMemory<byte> || value is Memory<byte>)
            return false;

        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum)
            return false;

        if (value is decimal or DateTime or DateTimeOffset or TimeSpan or Guid)
            return false;

        return true;
    }
}
