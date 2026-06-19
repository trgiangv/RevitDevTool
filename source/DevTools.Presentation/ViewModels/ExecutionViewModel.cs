namespace DevTools.Presentation.ViewModels;

public partial class ExecutionViewModel(CommandViewModel commandViewModel, PackageViewModel packageViewModel) : ObservableObject
{
    public CommandViewModel CommandViewModel { get; } = commandViewModel;
    public PackageViewModel PackageViewModel { get; } = packageViewModel;

    [ObservableProperty]
    public partial bool IsPackageMode { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public void ExecuteLastItem() => CommandViewModel.ExecuteLastItem();

    partial void OnSearchTextChanged(string value)
    {
        if (IsPackageMode)
            PackageViewModel.SearchText = value;
        else
            CommandViewModel.SearchText = value;
    }

    partial void OnIsPackageModeChanged(bool value)
    {
        if (value)
        {
            PackageViewModel.SearchText = SearchText;
            PackageViewModel.RefreshCommand.Execute(null);
        }
        else
        {
            CommandViewModel.SearchText = SearchText;
        }
    }

    [RelayCommand]
    private void ExpandAll()
    {
        if (IsPackageMode) PackageViewModel.ExpandAllCommand.Execute(null);
        else CommandViewModel.ExpandAllCommand.Execute(null);
    }

    [RelayCommand]
    private void CollapseAll()
    {
        if (IsPackageMode) PackageViewModel.CollapseAllCommand.Execute(null);
        else CommandViewModel.CollapseAllCommand.Execute(null);
    }

    [RelayCommand]
    private void ToggleAll()
    {
        if (IsPackageMode) PackageViewModel.ToggleAllCommand.Execute(null);
        else CommandViewModel.ToggleAllCommand.Execute(null);
    }
}
