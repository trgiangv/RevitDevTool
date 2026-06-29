using System.Buffers;
using System.Net.Http;
using DevTools.Logging.Abstractions;
using DevTools.Logging.Options;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;
namespace DevTools.Logging.Targets;

/// <summary>
/// Manages HTTP logging by dynamically adding/removing a <see cref="ZLoggerLogProcessorLoggerProvider"/>
/// wrapping a <see cref="HttpBatchProcessor"/> to the <see cref="ILoggerFactory"/> at runtime.
/// </summary>
public sealed class HttpLogProcessor(ILoggerFactory loggerFactory) : IHttpLogTarget
{
    private ZLoggerLogProcessorLoggerProvider? _provider;
    private bool _disposed;

    public void Enable<T>(T options)
    {
        if (options is not HttpLoggingOptions httpOptions)
            throw new ArgumentException($"Expected {nameof(HttpLoggingOptions)}, got {typeof(T).Name}");

        if (string.IsNullOrWhiteSpace(httpOptions.Endpoint))
        {
            Disable();
            return;
        }

        var zloggerOptions = new ZLoggerOptions { IncludeScopes = true };

        if (httpOptions.Format == SaveFormat.Json)
            zloggerOptions.UseJsonFormatter();
        else
            zloggerOptions.UsePlainTextFormatter();

        var processor = new HttpBatchProcessor(
            httpOptions.BatchSize > 0 ? httpOptions.BatchSize : 100,
            zloggerOptions,
            httpOptions.Endpoint);

        var newProvider = new ZLoggerLogProcessorLoggerProvider(processor, zloggerOptions);
        loggerFactory.AddProvider(newProvider);

        var old = Interlocked.Exchange(ref _provider, newProvider);
        old?.Dispose();
    }

    public void Disable()
    {
        var old = Interlocked.Exchange(ref _provider, null);
        old?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disable();
    }

    private sealed class HttpBatchProcessor(int batchSize, ZLoggerOptions options, string endpoint) : BatchingAsyncLogProcessor(batchSize, options)
    {
        private readonly HttpClient _httpClient = new();
        private readonly ArrayBufferWriter<byte> _buffer = new();
        private readonly IZLoggerFormatter _formatter = options.CreateFormatter();

        protected override async ValueTask ProcessAsync(IReadOnlyList<INonReturnableZLoggerEntry> list)
        {
            foreach (var item in list)
            {
                item.FormatUtf8(_buffer, _formatter);
            }

            var content = new ByteArrayContent(_buffer.WrittenMemory.ToArray());
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            try
            {
                await _httpClient.PostAsync(endpoint, content).ConfigureAwait(false);
            }
            catch
            {
                // Swallow HTTP errors to avoid crashing the logging pipeline.
            }
            finally
            {
                _buffer.Clear();
            }
        }

        protected override ValueTask DisposeAsyncCore()
        {
            _httpClient.Dispose();
            return default;
        }
    }
}
