using System.Globalization;

namespace DevTools.Ipc;

public static class McpPipeName
{
    private const string Prefix = "DevTools.Mcp.v2.";

    public static string Format(int processId)
    {
        if (processId <= 0)
            throw new ArgumentOutOfRangeException(nameof(processId));

        return Prefix + processId.ToString(CultureInfo.InvariantCulture);
    }

    public static bool TryParse(string name, out int processId)
    {
        processId = 0;
        return name.StartsWith(Prefix, StringComparison.Ordinal) &&
               int.TryParse(
                   name.Substring(Prefix.Length),
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out processId) &&
               processId > 0;
    }
}
