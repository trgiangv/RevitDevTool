namespace RevitDevTool.CodeExecute.Interfaces;

/// <summary>
/// Strategy interface for executing different types of code.
/// Implements Strategy Pattern for polymorphic execution.
/// Execution is fire-and-forget via ExternalEventController.
/// </summary>
public interface IExecutionStrategy
{
    /// <summary>
    /// Execute the code/script via ExternalEventController.
    /// </summary>
    void Execute();
}