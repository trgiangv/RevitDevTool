namespace RevitDevTool.Logging.Theme;

/// <summary>
/// Defines the different style tokens used in log output formatting.
/// This is a library-agnostic representation that can be mapped to
/// </summary>
public enum LogStyleToken
{
    Text,
    SecondaryText,
    TertiaryText,
    Invalid,
    Null,
    Name,
    String,
    Number,
    Boolean,
    Scalar,
    LevelVerbose,
    LevelDebug,
    LevelInformation,
    LevelWarning,
    LevelError,
    LevelFatal
}
