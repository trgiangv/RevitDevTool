namespace RevitDevTool.Settings.Options;

/// <summary>
/// Abstraction for configuration persistence operations.
/// </summary>
public interface IFileConfig<out TOptions> where TOptions : class
{
    /// <summary>
    /// Configuration options.
    /// </summary>
    TOptions Options { get; }

    /// <summary>
    /// Load configuration of type T
    /// </summary>
    /// <typeparam name="T">Configuration type.</typeparam>
    /// <returns>Loaded configuration or null if not found.</returns>
    T? Load<T>() where T : class;

    /// <summary>
    /// Save configuration
    /// </summary>
    /// <typeparam name="T">Configuration type.</typeparam>
    /// <param name="config">Configuration object to save.</param>
    void Save<T>(T config) where T : class;
}
