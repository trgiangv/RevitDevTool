using Color = System.Drawing.Color;

namespace RevitDevTool.Logging.Theme;

/// <summary>
/// Represents a style with foreground and background colors.
/// Library-agnostic representation for log styling.
/// </summary>
public readonly record struct LogStyle(Color Foreground, Color Background);
