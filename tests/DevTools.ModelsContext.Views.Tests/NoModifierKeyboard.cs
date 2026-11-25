using System.Windows.Input;

namespace DevTools.ModelsContext.Views.Tests;

internal sealed class NoModifierKeyboard : KeyboardDevice
{
    private static readonly Lazy<NoModifierKeyboard> Shared = new(() => WpfSta.Invoke(() => new NoModifierKeyboard()));

    private NoModifierKeyboard()
        : base(InputManager.Current)
    {
    }

    public static KeyboardDevice Device => Shared.Value;

    protected override KeyStates GetKeyStatesFromSystem(Key key)
    {
        return KeyStates.None;
    }
}
