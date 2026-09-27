// ReSharper disable once CheckNamespace
namespace DevTools.UI.Controls;

/// <summary>
/// When <see cref="ColorPicker"/> writes the selected color into the recent palette.
/// Adapted from MahApps.Metro.
/// </summary>
public enum AddToRecentColorsTrigger
{
    Never,
    ColorPickerClosed,
    SelectedColorChanged
}
