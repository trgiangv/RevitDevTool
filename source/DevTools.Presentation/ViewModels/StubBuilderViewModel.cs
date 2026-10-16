using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows.Data;
using DevTools.Presentation.Models;
using DevTools.Presentation.Services;
using PythonNetStubGenerator;
// ReSharper disable UnusedParameterInPartialMethod
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Presentation.ViewModels;

public partial class StubBuilderViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string OutputPath { get; set; }

    [ObservableProperty]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial bool HideUnchecked { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;
    private ObservableCollection<AssemblyItem> AppDomainAssemblies { get; } = [];
    public ICollectionView FilteredAssemblies { get; }
    private CancellationTokenSource? _searchDebounceToken;

    public StubBuilderViewModel(string versionNumber)
    {
        OutputPath = Path.Combine(Utilities.AppUtils.GetContentRootPath(versionNumber), "Stubs");
        FilteredAssemblies = CollectionViewSource.GetDefaultView(AppDomainAssemblies);
        FilteredAssemblies.Filter = FilterAssembly;
        LoadAppDomainAssemblies();
    }

    partial void OnHideUncheckedChanged(bool value) => FilteredAssemblies.Refresh();

    partial void OnSearchTextChanged(string value) => ApplySearchDebounce();

    private void ApplySearchDebounce()
    {
        _searchDebounceToken?.Cancel();
        _searchDebounceToken?.Dispose();
        _searchDebounceToken = new CancellationTokenSource();

        Task.Delay(300, _searchDebounceToken.Token).ContinueWith(_ =>
        {
            if (!_searchDebounceToken.Token.IsCancellationRequested)
                FilteredAssemblies.Refresh();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private bool FilterAssembly(object obj)
    {
        if (obj is not AssemblyItem item) return false;

        if (HideUnchecked && !item.IsSelected)
            return false;

        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        var search = SearchText.Trim();
        return item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               item.Location.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               item.FullName.Contains(search, StringComparison.OrdinalIgnoreCase);

    }

    private void LoadAppDomainAssemblies()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .OrderBy(a => a.GetName().Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => new AssemblyItem
            {
                Name = a.GetName().Name ?? "(unknown)",
                FullName = a.FullName!,
                Location = ResolveAssemblyLocationHint(a),
                Assembly = a
            });

        foreach (var item in assemblies)
            AppDomainAssemblies.Add(item);
    }

    /// <summary>
    /// Prefer <see cref="Assembly.Location"/>; when empty (common in some hosts), show module path so stubs can still be chosen.
    /// </summary>
    private static string ResolveAssemblyLocationHint(Assembly assembly)
    {
        if (!string.IsNullOrEmpty(assembly.Location))
            return assembly.Location;

        try
        {
            foreach (var module in assembly.GetModules(false))
            {
                var fq = module.FullyQualifiedName;
                if (!string.IsNullOrEmpty(fq) && File.Exists(fq))
                    return fq;
            }
        }
        catch
        {
            // ignored
        }

        return "(no disk path)";
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in AppDomainAssemblies)
            item.IsSelected = true;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var item in AppDomainAssemblies)
            item.IsSelected = false;
    }

    [RelayCommand]
    private void BrowseOutputPath()
    {
        var selectedPath = AppUtils.SelectFolder("Select Stub Output Folder");
        if (!string.IsNullOrEmpty(selectedPath))
            OutputPath = selectedPath;
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateStubsAsync()
    {
        if (IsGenerating) return;

        var selectedItems = AppDomainAssemblies
            .Where(a => a.IsSelected && a.Assembly != null)
            .ToArray();

        if (selectedItems.Length == 0)
        {
            StatusMessage = "Please select at least one assembly.";
            return;
        }

        await RunStubGenerationAsync(selectedItems).ConfigureAwait(true);
    }

    private async Task RunStubGenerationAsync(AssemblyItem[] items)
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            StatusMessage = "Please specify an output path.";
            return;
        }

        IsGenerating = true;
        StatusMessage = "Generating...";
        Progress = 0;
        GenerateStubsCommand.NotifyCanExecuteChanged();

        var outputPath = OutputPath;
        AssemblyDump[] dumps = [];
        try
        {
            var assemblies = items.Select(item => item.Assembly!).ToArray();
            var destPath = new DirectoryInfo(outputPath);
            await Task.Run(() =>
            {
                dumps = DumpPathlessAssemblies(items, outputPath);
                StubBuilder.BuildAssemblyStubs(destPath, assemblies);
            }).ConfigureAwait(true);

            PublishDumps(dumps);
            StatusMessage = $"Generated at: {outputPath}{DescribeDumpFailures(dumps)}";
        }
        catch (Exception ex)
        {
            PublishDumps(dumps);
            StatusMessage = $"Error: {ex.Message}{DescribeDumpFailures(dumps)}";
        }
        finally
        {
            IsGenerating = false;
            Progress = 100;
            GenerateStubsCommand.NotifyCanExecuteChanged();
        }
    }

    private AssemblyDump[] DumpPathlessAssemblies(AssemblyItem[] items, string outputPath)
    {
        var pathless = items.Where(item => item.Assembly != null && !File.Exists(item.Location)).ToArray();
        if (pathless.Length == 0)
            return [];

        var generated = AssemblyGenerator.Generate(
            pathless.Select(item => item.Assembly!).ToArray(),
            outputPath);

        var dumps = new AssemblyDump[generated.Length];
        for (var i = 0; i < generated.Length; i++)
        {
            var result = generated[i];
            var item = AppDomainAssemblies.FirstOrDefault(candidate => ReferenceEquals(candidate.Assembly, result.Assembly));
            var name = item?.Name ?? result.Assembly.GetName().Name ?? "(unknown)";
            dumps[i] = new AssemblyDump(item, name, result.OutputFile, result.Error);
        }

        return dumps;
    }

    private void PublishDumps(IReadOnlyList<AssemblyDump> dumps)
    {
        var changed = false;
        foreach (var dump in dumps)
        {
            if (dump.OutputFile == null || dump.Item == null)
                continue;
            dump.Item.Location = dump.OutputFile;
            changed = true;
        }

        if (changed)
            FilteredAssemblies.Refresh();
    }

    private static string DescribeDumpFailures(IReadOnlyList<AssemblyDump> dumps)
    {
        var failed = new List<AssemblyDump>();
        foreach (var dump in dumps)
        {
            if (dump.OutputFile == null)
                failed.Add(dump);
        }

        if (failed.Count == 0)
            return string.Empty;

        var names = string.Join(", ", failed.Take(3).Select(dump => dump.Name));
        if (failed.Count > 3)
            names += $" (+{failed.Count - 3} more)";
        return $" Could not dump {names}: {failed[0].Error}";
    }

    private sealed record AssemblyDump(AssemblyItem? Item, string Name, string? OutputFile, string? Error);

    private bool CanGenerate() => !IsGenerating;
}

