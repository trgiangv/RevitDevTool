namespace RevitDevTool.Messages;

/// <summary>
/// Published when user clicks Apply in Log Settings.
/// Consumers should re-read settings from <see cref="Services.ISettingsService"/>.
/// </summary>
public sealed record LogSettingsAppliedMessage;


