namespace RevitMcpToolSet.Utilities;

internal class OperationOutcome
{
    private readonly List<(bool success, string message, long elementId)> _results = [];

    public void Record(bool success, string message, long elementId)
        => _results.Add((success, message, elementId));

    public object Summarize(int maxErrorsToShow = 5)
    {
        var successes = _results.Count(r => r.success);
        var failures = _results.Where(r => !r.success).ToList();
        return new
        {
            outcome = failures.Count == 0 ? "Success" : "Partial",
            successCount = successes,
            failureCount = failures.Count,
            failures = failures.Take(maxErrorsToShow).Select(f => new { f.elementId, f.message }),
            additionalFailures = Math.Max(0, failures.Count - maxErrorsToShow),
        };
    }
}
