using RevitDevTool.Core;

namespace RevitDevTool.Tools.ModelsContext;

internal static class RevitCall
{
    public const string Busy = "Revit is busy.";
    public const string NotOpen = "This model is not open.";
    public const string NoActiveView = "There is no active view.";

    public static async Task<T> RunAsync<T>(Func<UIApplication, T> work, CancellationToken cancellationToken)
    {
        try
        {
            return await RevitContextExecutor.RaiseAsync(work, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(exception.Message);
        }
    }

    public static Task RunAsync(Action<UIApplication> work, CancellationToken cancellationToken)
    {
        return RunAsync(application =>
        {
            work(application);
            return true;
        }, cancellationToken);
    }

    public static void EnsureEditable(Document document)
    {
        if (document.IsModifiable || document.IsReadOnly)
        {
            throw new InvalidOperationException(Busy);
        }
    }
}

internal static class RevitIds
{
    public static long ValueOf(ElementId id)
    {
#if REVIT2024_OR_GREATER
        return id.Value;
#else
        return id.IntegerValue;
#endif
    }

    public static ElementId From(long value)
    {
#if REVIT2024_OR_GREATER
        return new ElementId(value);
#else
        return new ElementId((int)value);
#endif
    }

    public static List<ElementId> From(IEnumerable<long> values)
    {
        return values.Select(From).ToList();
    }

    public static List<long> ValuesOf(IEnumerable<ElementId> ids)
    {
        return ids.Select(ValueOf).ToList();
    }
}
