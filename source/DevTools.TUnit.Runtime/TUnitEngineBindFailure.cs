using Microsoft.Testing.Platform.Extensions.Messages;
using TUnit.Engine.Exceptions;

namespace DevTools.TUnit.Runtime;

/// <summary>
/// TUnit.Engine catches what escapes its run and publishes it as an error node, so a
/// bind failure inside the engine would read as a test result. This returns the
/// exception that node carries when it is a bind failure of TUnit or the platform.
/// </summary>
internal static class TUnitEngineBindFailure
{
    public static Exception? TryRead(TestNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var exception = node.Properties.SingleOrDefault<ErrorTestNodeStateProperty>()?.Exception;
        while (exception is TUnitFailedException { WrappedException: { } wrapped })
            exception = wrapped;

        if (exception is not (MissingMemberException or TypeLoadException or FileNotFoundException or FileLoadException))
            return null;

        return exception.Message.Contains("TUnit.", StringComparison.Ordinal)
               || exception.Message.Contains("Microsoft.Testing.", StringComparison.Ordinal)
            ? exception
            : null;
    }
}
