using RevitDevTool.Core.Handlers;

namespace RevitDevTool.Core;

/// <summary>
///     Dispatches delegates to the Revit main thread via <see cref="ExternalEvent"/>.
/// </summary>
/// <remarks>
///     <para>
///         <c>Raise</c> methods are fire-and-forget; exceptions are traced but not propagated.<br/>
///         <c>RaiseAsync</c> methods return a <see cref="Task"/> that completes (or faults)
///         when the delegate finishes on the Revit thread.
///     </para>
///     <para>
///         All requests are processed <b>FIFO</b>. If the caller is already on the Revit thread
///         inside an API context and no requests are pending, the delegate executes synchronously
///         without queuing.
///     </para>
/// </remarks>
[PublicAPI]
public static class RevitContextExecutor
{
    private static readonly RevitDispatcher Dispatcher = new();

    /// <summary>
    ///     Queues <paramref name="action"/> for fire-and-forget execution on the Revit thread.
    /// </summary>
    /// <param name="action">
    ///     The delegate to execute. Receives the current <see cref="UIApplication"/>.
    /// </param>
    /// <exception cref="System.ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public static void Raise(Action<UIApplication> action)
    {
        Dispatcher.Post(action);
    }

    /// <inheritdoc cref="Raise(System.Action{Autodesk.Revit.UI.UIApplication})"/>
    /// <param name="action">The delegate to execute.</param>
    public static void Raise(Action action)
    {
        Dispatcher.Post(action);
    }

    /// <summary>
    ///     Queues <paramref name="action"/> for execution on the Revit thread and returns a
    ///     <see cref="Task"/> that completes when the delegate finishes.
    /// </summary>
    /// <param name="action">
    ///     The delegate to execute. Receives the current <see cref="UIApplication"/>.
    /// </param>
    /// <param name="token">Optional cancellation token.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public static Task RaiseAsync(Action<UIApplication> action, CancellationToken token = default)
    {
        return Dispatcher.InvokeAsync(action, token);
    }

    /// <inheritdoc cref="RaiseAsync(Action{Autodesk.Revit.UI.UIApplication}, CancellationToken)"/>
    /// <param name="action">The delegate to execute.</param>
    /// <param name="token">Optional cancellation token.</param>
    public static Task RaiseAsync(Action action, CancellationToken token = default)
    {
        return Dispatcher.InvokeAsync(action, token);
    }

    /// <summary>
    ///     Queues <paramref name="handler"/> for execution on the Revit thread and returns a
    ///     <see cref="Task{T}"/> containing the result.
    /// </summary>
    /// <typeparam name="T">Return type of <paramref name="handler"/>.</typeparam>
    /// <param name="handler">
    ///     The delegate to execute. Receives the current <see cref="UIApplication"/>.
    /// </param>
    /// <param name="token">Optional cancellation token.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    public static Task<T> RaiseAsync<T>(Func<UIApplication, T> handler, CancellationToken token = default)
    {
        return Dispatcher.InvokeAsync(handler, token);
    }

    /// <inheritdoc cref="RaiseAsync{T}(Func{Autodesk.Revit.UI.UIApplication, T}, CancellationToken)"/>
    /// <param name="handler">The delegate to execute.</param>
    /// <param name="token">Optional cancellation token.</param>
    public static Task<T> RaiseAsync<T>(Func<T> handler, CancellationToken token = default)
    {
        return Dispatcher.InvokeAsync(handler, token);
    }

    /// <summary>
    ///     Queues an async delegate for execution on the Revit thread and returns a
    ///     <see cref="Task{T}"/> containing the result.
    ///     Revit API calls must occur in the synchronous part of <paramref name="asyncHandler"/>
    ///     before the first <see langword="await"/>.
    /// </summary>
    /// <typeparam name="T">Return type of <paramref name="asyncHandler"/>.</typeparam>
    /// <param name="asyncHandler">The async delegate to execute.</param>
    /// <param name="token">Optional cancellation token.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="asyncHandler"/> is <see langword="null"/>.</exception>
    public static Task<T> RaiseAsync<T>(Func<Task<T>> asyncHandler, CancellationToken token = default)
    {
        return Dispatcher.InvokeAsync(asyncHandler, token);
    }

    /// <inheritdoc cref="RaiseAsync{T}(Func{Task{T}}, CancellationToken)"/>
    /// <param name="asyncHandler">
    ///     The async delegate to execute. Receives the current <see cref="UIApplication"/>.
    /// </param>
    /// <param name="token">Optional cancellation token.</param>
    public static Task<T> RaiseAsync<T>(Func<UIApplication, Task<T>> asyncHandler, CancellationToken token = default)
    {
        return Dispatcher.InvokeAsync(asyncHandler, token);
    }

    /// <summary>
    ///     Queues an async action (no return value) for execution on the Revit thread.
    ///     Revit API calls must occur before the first <see langword="await"/>.
    /// </summary>
    /// <param name="asyncAction">The async delegate to execute.</param>
    /// <param name="token">Optional cancellation token.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="asyncAction"/> is <see langword="null"/>.</exception>
    public static Task RaiseAsync(Func<Task> asyncAction, CancellationToken token = default)
    {
        return Dispatcher.InvokeAsync(asyncAction, token);
    }
}
