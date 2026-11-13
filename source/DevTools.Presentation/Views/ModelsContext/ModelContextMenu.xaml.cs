using DevTools.ModelsContext.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace DevTools.Presentation.Views.ModelsContext;

public partial class ModelContextMenu : ContextMenu
{
    public ModelContextMenu()
    {
        InitializeComponent();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (DataContext is not ModelContextViewModel panel || e.Key is not (Key.Z or Key.B or Key.I or Key.H))
        {
            return;
        }

        e.Handled = true;
        IsOpen = false;
        _ = panel.ContextMenu.RunShortcutAsync(e.Key.ToString());
    }
}
