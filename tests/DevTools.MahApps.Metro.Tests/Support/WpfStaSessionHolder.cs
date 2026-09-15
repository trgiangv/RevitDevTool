namespace DevTools.MetroFork.Tests.Support;

/// <summary>Process-wide STA WPF session. Do not create a second Application in this testhost.</summary>
public static class WpfStaSessionHolder
{
    public static WpfStaSession Instance { get; } = new();
}
