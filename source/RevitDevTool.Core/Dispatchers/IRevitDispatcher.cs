namespace RevitDevTool.Core.Dispatchers;

public interface IRevitDispatcher
{
    /// <remarks>
    ///     These methods schedule work into a Revit API callback. The returned <see cref="Task"/>
    ///     signals completion of the scheduled delegate only; it does not extend the API-context
    ///     boundary beyond that delegate. If the delegate performs its own awaited work, any later
    ///     Revit API access must re-enter through another dispatcher call.
    /// </remarks>
    /// <summary>
    /// Queues a fire-and-forget action for execution in the Revit API context.
    /// Exceptions are traced but not propagated.
    /// </summary>
    void Post(Action<UIApplication> action);

    /// <summary>
    /// Queues a fire-and-forget action for execution in the Revit API context.
    /// Exceptions are traced but not propagated.
    /// </summary>
    void Post(Action action);

    /// <summary>
    /// Queues an action for execution in the Revit API context.
    /// The returned task completes when the action finishes.
    /// Exceptions are propagated through the task.
    /// </summary>
    Task InvokeAsync(Action<UIApplication> action, CancellationToken token = default);

    /// <summary>
    /// Queues an action for execution in the Revit API context.
    /// The returned task completes when the action finishes.
    /// Exceptions are propagated through the task.
    /// </summary>
    Task InvokeAsync(Action action, CancellationToken token = default);

    /// <summary>
    /// Queues a function for execution in the Revit API context.
    /// The returned task completes with the function result.
    /// Exceptions are propagated through the task.
    /// </summary>
    Task<T> InvokeAsync<T>(Func<UIApplication, T> handler, CancellationToken token = default);

    /// <summary>
    /// Queues a function for execution in the Revit API context.
    /// The returned task completes with the function result.
    /// Exceptions are propagated through the task.
    /// </summary>
    Task<T> InvokeAsync<T>(Func<T> handler, CancellationToken token = default);
}