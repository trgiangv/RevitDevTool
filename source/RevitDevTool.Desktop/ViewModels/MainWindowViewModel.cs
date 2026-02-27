using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;

namespace RevitDevTool.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private PageViewModelBase _activePage;

    public IReadOnlyList<PageViewModelBase> Pages { get; }

    /// <summary>Pages shown in the side menu (excludes hidden pages like Settings).</summary>
    public IReadOnlyList<PageViewModelBase> MenuPages { get; }

    private readonly SettingsPageViewModel _settingsPage;
    private PageViewModelBase? _previousPage;

    public MainWindowViewModel(
        ProcessorPageViewModel processorPage,
        AssistantPageViewModel assistantPage,
        DataPageViewModel dataPage,
        SettingsPageViewModel settingsPage)
    {
        _settingsPage = settingsPage;
        Pages = [processorPage, assistantPage, dataPage, settingsPage];
        MenuPages = Pages.Where(p => p.IsVisibleOnSideMenu).ToList();
        ActivePage = processorPage;
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        _previousPage = ActivePage;
        ActivePage = _settingsPage;
        _settingsPage.CanGoBack = true;
    }

    [RelayCommand]
    private void NavigateBack()
    {
        if (_previousPage != null)
        {
            ActivePage = _previousPage;
            _previousPage = null;
            _settingsPage.CanGoBack = false;
        }
    }
}
