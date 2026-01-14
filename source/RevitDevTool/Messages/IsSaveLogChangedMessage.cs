using CommunityToolkit.Mvvm.Messaging.Messages;
namespace RevitDevTool.Messages;

public class IsSaveLogChangedMessage(bool value) : ValueChangedMessage<bool>(value);