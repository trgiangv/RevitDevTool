using DevTools.ModelsContext.Documents;
using System.Reflection;

namespace DevTools.ModelsContext.Sandbox;

public class SlowSource : DispatchProxy
{
    private IModelSource _inner = null!;
    private Func<TimeSpan> _delay = () => TimeSpan.Zero;

    public static IModelSource Wrap(IModelSource inner, Func<TimeSpan> delay)
    {
        var proxy = Create<IModelSource, SlowSource>();
        var slow = (SlowSource)(object)proxy;
        slow._inner = inner;
        slow._delay = delay;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        var type = targetMethod.ReturnType;
        var readsSnapshot = targetMethod.Name == nameof(IModelSource.GetInstanceNamesAsync) && _delay() <= TimeSpan.Zero;
        if (readsSnapshot || !type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Task<>))
        {
            return Unwrap(() => targetMethod.Invoke(_inner, args));
        }

        var run = typeof(SlowSource).GetMethod(nameof(RunAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(type.GetGenericArguments()[0]);
        return run.Invoke(this, [targetMethod, args]);
    }

    private static object? Unwrap(Func<object?> call)
    {
        try
        {
            return call();
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private async Task<T> RunAsync<T>(MethodInfo method, object?[]? args)
    {
        var delay = _delay();
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay).ConfigureAwait(false);
        }

        return await Task.Run(() => (Task<T>)Unwrap(() => method.Invoke(_inner, args))!).ConfigureAwait(false);
    }
}
