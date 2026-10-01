using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Runtime;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.OutputDevice;

namespace DevTools.MSTest.MTP;

/// <summary>
/// The testhost's only reach into MTP internals. <c>--list-tests json</c> does not publish
/// nodes to a data consumer: the platform output device buffers them and prints JSON to the
/// process stdout. Both the buffer and that stdout writer are private to MTP 2.4.1, so they
/// are read through <see cref="InternalMembers"/> (cached) and nowhere else.
/// </summary>
internal static class MtpDiscoveryInternals
{
    private const string NodeBufferField = "_discoveredTestsForJson";
    private const string ConsoleTypeName = "Microsoft.Testing.Platform.Helpers.SystemConsole";

    public static IReadOnlyList<TestNode> ReadDiscoveredNodes(ITestApplication application)
    {
        var host = InternalMembers.TryField(application.GetType(), "_host")?.GetValue(application)
            ?? throw new TestingDiscoveryFailedException("MSTest TestApplication did not expose its host.");
        if (InternalMembers.TryProperty(host.GetType(), "ServiceProvider")?.GetValue(host)
            is not IServiceProvider services)
        {
            throw new TestingDiscoveryFailedException(
                "MSTest TestApplication host did not expose a service provider.");
        }

        var terminal = FindNodeBuffer(services.GetService(typeof(IOutputDevice)))
            ?? throw new TestingDiscoveryFailedException(
                "MSTest --list-tests did not expose discovered test nodes.");
        if (InternalMembers.Field(terminal.GetType(), NodeBufferField).GetValue(terminal) is not IEnumerable nodes)
        {
            throw new TestingDiscoveryFailedException(
                "MSTest --list-tests did not expose discovered test nodes.");
        }

        return nodes.OfType<TestNode>().ToList();
    }

    /// <summary>
    /// The buffering device sits behind any number of wrappers; walk <c>OriginalOutputDevice</c> to it.
    /// </summary>
    private static object? FindNodeBuffer(object? device)
    {
        for (var current = device; current is not null;)
        {
            if (InternalMembers.TryField(current.GetType(), NodeBufferField) is not null)
                return current;

            var next = InternalMembers.TryProperty(current.GetType(), "OriginalOutputDevice")?.GetValue(current);
            if (next is null || ReferenceEquals(next, current))
                return null;
            current = next;
        }

        return null;
    }

    /// <summary>
    /// Points MTP's captured stdout writer at a null sink for the duration of a discovery, so
    /// the JSON listing never reaches the testhost's own stdout. Best effort: a missing or
    /// read-only field only means noisier output, never a failed discovery.
    /// </summary>
    public sealed class ConsoleSilence : IDisposable
    {
        private readonly FieldInfo? _field;
        private readonly object? _original;
        private readonly StreamWriter? _capture;

        private ConsoleSilence(FieldInfo? field, object? original, StreamWriter? capture)
        {
            _field = field;
            _original = original;
            _capture = capture;
        }

        public static ConsoleSilence Begin()
        {
            RuntimeHelpers.RunClassConstructor(typeof(TestApplication).TypeHandle);
            var consoleType = typeof(TestApplication).Assembly.GetType(ConsoleTypeName);
            var field = consoleType is null
                ? null
                : InternalMembers.TryField(consoleType, "CaptureConsoleOutWriter", isStatic: true);
            if (field?.GetValue(null) is not StreamWriter original)
                return new ConsoleSilence(null, null, null);

            var capture = new StreamWriter(Stream.Null, original.Encoding, 256, leaveOpen: true)
            {
                AutoFlush = true,
            };
            try
            {
                field.SetValue(null, capture);
            }
            catch (Exception exception) when (exception is FieldAccessException or NotSupportedException)
            {
                capture.Dispose();
                return new ConsoleSilence(null, null, null);
            }

            return new ConsoleSilence(field, original, capture);
        }

        public void Dispose()
        {
            _field?.SetValue(null, _original);
            _capture?.Dispose();
        }
    }
}
