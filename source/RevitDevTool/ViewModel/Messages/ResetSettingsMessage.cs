using CommunityToolkit.Mvvm.Messaging.Messages;
namespace RevitDevTool.ViewModel.Messages;

/// <summary>
/// Message sent when settings are reset to notify all settings ViewModels to reload from config.
/// </summary>
public sealed class ResetSettingsMessage() : ValueChangedMessage<bool>(true);
