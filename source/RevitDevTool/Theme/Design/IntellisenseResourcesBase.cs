using System.ComponentModel;
using System.Windows;
namespace RevitDevTool.Theme.Design;

/// <summary>
/// Base class for design-time intellisense resources.
/// Only loads resources when in design mode, clears them at runtime.
/// </summary>
public abstract class IntellisenseResourcesBase : ResourceDictionary, ISupportInitialize
{
    public new Uri Source
    {
        get => base.Source;
        set
        {
            if (DesignMode.IsDesignModeEnabled)
            {
                base.Source = value;
            }
        }
    }

    private new void EndInit()
    {
        Clear();
        MergedDictionaries.Clear();
        base.EndInit();
    }

    void ISupportInitialize.EndInit()
    {
        EndInit();
    }
}
