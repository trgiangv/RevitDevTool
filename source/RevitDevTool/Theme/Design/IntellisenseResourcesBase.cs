using System.ComponentModel;
using System.Windows;

namespace RevitDevTool.Theme.Design;

/// <summary>
/// Base class for design-time intellisense resources.
/// Only loads resources when in design mode, clears them at runtime to avoid duplicates.
/// </summary>
/// <remarks>
/// This class provides XAML intellisense support in XAML designers.
/// At design time, it loads the specified resources for preview rendering.
/// At runtime, it clears all resources since they are already loaded by ThemeResources.
/// </remarks>
public abstract class IntellisenseResourcesBase : ResourceDictionary, ISupportInitialize
{
    /// <summary>
    /// Gets or sets the source URI for the resource dictionary.
    /// Only loads the source when in design mode.
    /// </summary>
    [UsedImplicitly]
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
        if (!DesignMode.IsDesignModeEnabled)
        {
            Clear();
            MergedDictionaries.Clear();
        }
        base.EndInit();
    }

    void ISupportInitialize.EndInit()
    {
        EndInit();
    }
}
