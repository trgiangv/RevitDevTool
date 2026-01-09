namespace RevitDevTool.Logging;

/// <summary>
/// Abstraction for UI log output destinations such as RichTextBox or other controls.
/// Enables swapping output implementations without changing core logging logic.
/// </summary>
public interface ILogOutputSink : IDisposable
{
    void Clear();
    void SetTheme(bool isDarkTheme);
    object GetHostControl();
}

