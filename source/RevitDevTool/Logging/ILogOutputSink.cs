namespace RevitDevTool.Logging;

/// <summary>
/// Abstraction for UI log output destinations such as RichTextBox or other controls.
/// Enables swapping output implementations without changing core logging logic.
/// </summary>
public interface ILogOutputSink : IDisposable
{
    /// <summary>
    /// Clears all log entries from the output sink.
    /// </summary>
    void Clear();

    /// <summary>
    /// Sets the theme of the log output sink.
    /// </summary>
    /// <param name="isDarkTheme">Indicates whether to use a dark theme.</param>
    void SetTheme(bool isDarkTheme);

    /// <summary>
    /// Gets the underlying host control used for log output.
    /// </summary>
    object GetHostControl();
}

