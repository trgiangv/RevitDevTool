using CommunityToolkit.Mvvm.Messaging.Messages;

namespace RevitDevTool.ViewModel.Messages;

public class GeometryCountChangedMessage(int value) : ValueChangedMessage<int>(value);
