namespace RevitDevTool.Logging;

/// <summary>
/// Abstraction for UI log output destinations such as RichTextBox or other controls.
/// Enables swapping output implementations without changing core logging logic.
/// </summary>
public interface ILoggingMonitor : IDisposable
{
    void Clear();
    object GetHostControl();
}
