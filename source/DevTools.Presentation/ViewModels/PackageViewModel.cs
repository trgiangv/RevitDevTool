using System.Collections.ObjectModel;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Python;
using DevTools.UI.Theme;
// ReSharper disable UnusedParameterInPartialMethod

namespace DevTools.Presentation.ViewModels;

public partial class PackageViewModel : ObservableObject, IBusyViewModel
{
    private readonly IPackageService _packageService;
    private readonly PythonInitializer _pythonInitializer;
    private IReadOnlyList<Package> _allPackages = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string BusyMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial PackageTreeNode? SelectedNode { get; set; }
    public ObservableCollection<PackageTreeNode> FilteredItems { get; } = [];

    public PackageViewModel(IPackageService packageService, PythonInitializer pythonInitializer)
    {
        _packageService = packageService;
        _pythonInitializer = pythonInitializer;
        ThemeManager.Current.ActualApplicationThemeChanged += OnThemeChanged;
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadPackagesAsync();

    [RelayCommand] private void ExpandAll() => ExecutionTreeViewHelper.ExpandAll(FilteredItems);
    [RelayCommand] private void CollapseAll() => ExecutionTreeViewHelper.CollapseAll(FilteredItems);
    [RelayCommand] private void ToggleAll() => ExecutionTreeViewHelper.ToggleAll(FilteredItems);

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private async Task RemoveAsync()
    {
        if (SelectedNode is not PackageItemNode packageNode) return;
        await this.WhileBusy($"Removing {packageNode.PackageId}...", async () =>
        {
            await _packageService.RemovePackageAsync(packageNode.ToRuntimePackage());
            await LoadPackagesAsync();
        });
    }

    private bool CanRemove() => SelectedNode is PackageItemNode { IsProtected: false };

    [RelayCommand(CanExecute = nameof(CanRemoveAll))]
    private async Task RemoveAllAsync()
    {
        if (SelectedNode is MarketplaceNode marketplaceNode)
        {
            await this.WhileBusy($"Removing all packages from {marketplaceNode.Name}...", async () =>
            {
                await _packageService.RemoveAllAsync(marketplaceNode.Marketplace);
                await LoadPackagesAsync();
            });
            return;
        }

        await this.WhileBusy("Removing all runtime packages...", async () =>
        {
            await _packageService.RemoveAllAsync(Marketplace.NuGet);
            if (_pythonInitializer.Provider?.Backend == PythonBackend.Pixi)
                await _packageService.RemoveAllAsync(Marketplace.CondaForge);
            await _packageService.RemoveAllAsync(Marketplace.PyPi);
            await LoadPackagesAsync();
        });
    }

    private bool CanRemoveAll() => SelectedNode switch
    {
        PackageItemNode => false,
        MarketplaceNode marketplace => marketplace.Children.OfType<PackageItemNode>().Any(item => !item.IsProtected),
        _ => FilteredItems.SelectMany(node => node.Children.OfType<PackageItemNode>()).Any(item => !item.IsProtected)
    };

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private async Task UpdateAsync()
    {
        if (SelectedNode is not PackageItemNode packageNode) return;
        await this.WhileBusy($"Updating {packageNode.PackageId} to latest...", async () =>
        {
            await _packageService.UpdateLatestAsync(packageNode.ToRuntimePackage());
            await LoadPackagesAsync();
        });
    }

    private bool CanUpdate() => SelectedNode is PackageItemNode { IsLatest: false };

    [RelayCommand(CanExecute = nameof(CanRepair))]
    private async Task RepairAsync()
    {
        if (SelectedNode is not PackageItemNode packageNode) return;
        await this.WhileBusy($"Repairing {packageNode.PackageId}...", async () =>
        {
            await _packageService.RepairAsync(packageNode.ToRuntimePackage());
            await LoadPackagesAsync();
        });
    }

    private bool CanRepair() => SelectedNode is PackageItemNode;

    private async Task LoadPackagesAsync()
    {
        await this.WhileBusy("Loading packages...", async () =>
        {
            _allPackages = await _packageService.ListInstalledPackagesAsync();
            RefreshFilteredItems();
        });
    }

    private void RefreshFilteredItems()
    {
        FilteredItems.Clear();
        var roots = BuildMarketplaceRoots();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            foreach (var node in roots)
            {
                ExecutionTreeViewHelper.SetVisibilityRecursive(node, true);
                ExecutionTreeViewHelper.ClearHighlightsRecursive(node);
                FilteredItems.Add(node);
            }
            return;
        }

        foreach (var node in roots)
        {
            if (ExecutionTreeViewHelper.FilterNodeRecursive(node, SearchText, ThemeManager.Current.ActualApplicationTheme == AppTheme.Dark))
                FilteredItems.Add(node);
        }
    }

    partial void OnSearchTextChanged(string value) => RefreshFilteredItems();

    partial void OnSelectedNodeChanged(PackageTreeNode? value)
    {
        RemoveCommand.NotifyCanExecuteChanged();
        RemoveAllCommand.NotifyCanExecuteChanged();
        UpdateCommand.NotifyCanExecuteChanged();
        RepairCommand.NotifyCanExecuteChanged();
    }

    private List<MarketplaceNode> BuildMarketplaceRoots()
    {
        var isPixi = _pythonInitializer.Provider?.Backend == PythonBackend.Pixi;
        Marketplace[] marketplaces = isPixi
            ? [Marketplace.NuGet, Marketplace.CondaForge, Marketplace.PyPi]
            : [Marketplace.NuGet, Marketplace.PyPi];

        var roots = new List<MarketplaceNode>(marketplaces.Length);
        foreach (var marketplace in marketplaces)
        {
            var root = new MarketplaceNode(marketplace) { IsExpanded = true };
            var packages = _allPackages
                .Where(item => item.Marketplace == marketplace)
                .OrderBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Version ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            foreach (var package in packages)
                root.Children.Add(new PackageItemNode(package));
            roots.Add(root);
        }
        return roots;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(SearchText)) RefreshFilteredItems();
    }

}
