namespace DevTools.NUnit.Host.Logging;

public static class NUnitOutputMerger
{
    public static string? Merge(string? nunitOutput, string? traceOutput)
    {
        var hasNunit = !string.IsNullOrWhiteSpace(nunitOutput);
        var hasTrace = !string.IsNullOrWhiteSpace(traceOutput);

        if (hasNunit && hasTrace)
            return nunitOutput!.TrimEnd() + Environment.NewLine + traceOutput!.TrimEnd();

        if (hasNunit)
            return nunitOutput;

        return hasTrace ? traceOutput : null;
    }
}
