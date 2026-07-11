using RevitMcpToolSet.Data;

namespace RevitMcpToolSet.Utilities;

internal class OperationOutcome
{
    private int _successCount;
    private readonly List<ToolError> _failures = [];

    public void RecordSuccess() => _successCount++;

    public void Record(bool success, string message, long elementId)
    {
        if (success)
            _successCount++;
        else
            _failures.Add(ToolErrorHelper.FromMessage(message, elementId));
    }

    public void RecordFailure(long elementId, Exception ex)
        => _failures.Add(ToolErrorHelper.FromException(ex, elementId));

    public void RecordFailure(long elementId, string message)
        => _failures.Add(ToolErrorHelper.FromMessage(message, elementId));

    public void RecordFailure(ToolError error)
        => _failures.Add(error);

    public object Summarize()
        => new
        {
            success_count = _successCount,
            failure_count = _failures.Count,
            failures = _failures.Count > 0 ? _failures : null,
        };

    public int SuccessCount => _successCount;
    public IReadOnlyList<ToolError> Failures => _failures;
}
