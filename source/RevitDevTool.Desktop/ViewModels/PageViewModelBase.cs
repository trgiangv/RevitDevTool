using CommunityToolkit.Mvvm.ComponentModel;
using Material.Icons;

namespace RevitDevTool.Desktop.ViewModels;

public abstract partial class PageViewModelBase : ViewModelBase
{
    /// <summary>
    /// The index of the page.
    /// </summary>
    public abstract int Index { get; }

    /// <summary>
    /// The display name of the page.
    /// </summary>
    public abstract string DisplayName { get; }

    /// <summary>
    /// The icon of the page.
    /// </summary>
    public abstract MaterialIconKind Icon { get; }

    /// <summary>
    /// The visibility of the page on the side menu.
    /// </summary>
    [ObservableProperty]
    private bool _isVisibleOnSideMenu = true;

    /// <summary>
    /// Whether a back button should be shown (used by Settings page).
    /// </summary>
    [ObservableProperty]
    private bool _canGoBack;
}

