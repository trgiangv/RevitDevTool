namespace DevTools.Mcp.Server.Contracts;

/// <summary>UTF-8 byte budgets for <c>invoke_dynamic</c> batch <c>reads[]</c> responses.</summary>
public static class InvokeDynamicLimits
{
    public const int DefaultResultBudgetBytes = 1024 * 1024;
    public const int HardResultBudgetBytes = 4 * 1024 * 1024;
}
