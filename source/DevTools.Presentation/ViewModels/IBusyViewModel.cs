using System.Runtime.CompilerServices;

namespace DevTools.Presentation.ViewModels;

/// <summary>
/// Reusable busy-indicator contract for ViewModels.
/// Use <see cref="BusyViewModelExtensions"/> extension methods for clean call sites.
/// </summary>
public interface IBusyViewModel
{
    bool IsBusy { get; set; }
    string BusyMessage { get; set; }
}

/// <summary>
/// Disposable scope with built-in depth tracking.
/// Nested scopes are safe — only the outermost dispose resets state.
/// </summary>
public sealed class BusyScope<T> : IDisposable where T : class, IBusyViewModel
{
    private T? _owner;
    private static readonly ConditionalWeakTable<object, DepthBox> DepthTable = new();

    private sealed class DepthBox { public int Value; }

    internal BusyScope(T owner, string message)
    {
        _owner = owner;
        var box = DepthTable.GetOrCreateValue(owner);
        box.Value++;
        owner.IsBusy = true;
        owner.BusyMessage = message;
    }

    public void Dispose()
    {
        if (_owner is null) return;
        var box = DepthTable.GetOrCreateValue(_owner);
        box.Value = Math.Max(0, box.Value - 1);
        if (box.Value == 0)
        {
            _owner.IsBusy = false;
            _owner.BusyMessage = string.Empty;
        }
        _owner = null;
    }
}

public static class BusyViewModelExtensions
{
    extension<T>(T vm) where T : class, IBusyViewModel
    {
        public async Task WhileBusy(string message, Func<Task> action)
        {
            using var scope = new BusyScope<T>(vm, message);
            await action().ConfigureAwait(true);
        }
        public void WhileBusy(string message, Action action)
        {
            using var scope = new BusyScope<T>(vm, message);
            action();
        }
    }
}
