using CommunityToolkit.Mvvm.Messaging.Messages;

namespace RevitDevTool.ViewModel.Messages;

public class IsSaveLogChangedMessage(bool value) : ValueChangedMessage<bool>(value);