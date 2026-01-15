using RevitDevTool.Settings;

namespace RevitDevTool.ViewModel.Messages;

/// <summary>
/// Published when user clicks Apply in Log Settings.
/// Consumers should re-read settings from <see cref="ISettingsService"/>.
/// </summary>
public sealed record LogSettingsAppliedMessage;


