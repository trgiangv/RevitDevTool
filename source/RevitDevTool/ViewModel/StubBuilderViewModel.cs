using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Threading;
using PythonNetStubGenerator;
using RevitDevTool.Utils;
// ReSharper disable UnusedParameterInPartialMethod

namespace RevitDevTool.ViewModel;

public partial class StubBuilderViewModel : ObservableObject
{
    private readonly DispatcherTimer _searchDebounceTimer;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _outputPath;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private double _progress;

    /// <summary>
    /// All assemblies from the current AppDomain.
    /// </summary>
    private ObservableCollection<AssemblyItem> AppDomainAssemblies { get; } = [];

    /// <summary>
    /// Filtered view of AppDomainAssemblies based on search text.
    /// </summary>
    public ICollectionView FilteredAssemblies { get; }

    /// <summary>
    /// Close action to be set by the window code-behind.
    /// </summary>
    public Action? CloseAction { get; set; }

    public StubBuilderViewModel()
    {
        _outputPath = Path.Combine(SettingsUtils.GetContentRootPath(), "Stubs");

        FilteredAssemblies = CollectionViewSource.GetDefaultView(AppDomainAssemblies);
        FilteredAssemblies.Filter = FilterAssembly;

        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            FilteredAssemblies.Refresh();
        };

        LoadAppDomainAssemblies();
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private bool FilterAssembly(object obj)
    {
        if (obj is not AssemblyItem item) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;

        return item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
               || item.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void LoadAppDomainAssemblies()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            // .Where(a =>
            // {
            //     try { return AssemblyLoader.IsManagedAssembly(a.Location); }
            //     catch { return false; }
            // })
            .OrderBy(a => a.GetName().Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => new AssemblyItem
            {
                Name = a.GetName().Name ?? Path.GetFileNameWithoutExtension(a.Location),
                FullName = a.FullName!,
                Location = a.Location,
                Assembly = a
            });

        foreach (var item in assemblies)
        {
            AppDomainAssemblies.Add(item);
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in AppDomainAssemblies)
        {
            if (FilterAssembly(item))
                item.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var item in AppDomainAssemblies)
        {
            item.IsSelected = false;
        }
    }

    [RelayCommand]
    private void BrowseOutputPath()
    {
        var selectedPath = SettingsUtils.SelectFolder("Select Stub Output Folder");
        if (!string.IsNullOrEmpty(selectedPath))
        {
            OutputPath = selectedPath;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateStubsAsync()
    {
        if (IsGenerating) return;

        var selectedAssemblies = AppDomainAssemblies
            .Where(a => a.IsSelected && a.Assembly != null)
            .Select(a => a.Assembly!)
            .ToArray();

        if (selectedAssemblies.Length == 0)
        {
            StatusMessage = "Please select at least one assembly.";
            return;
        }

        await RunStubGenerationAsync(selectedAssemblies).ConfigureAwait(true);
    }

    /// <summary>
    /// Opens a file dialog to select external DLLs, then generates stubs immediately.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateExternalStubsAsync()
    {
        if (IsGenerating) return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select DLL files for stub generation",
            Filter = "DLL files (*.dll)|*.dll|All files (*.*)|*.*",
            Multiselect = true
        };
        
        if (dialog.ShowDialog(UIFramework.MainWindow.getMainWnd()) != true) return;

        var dllPaths = dialog.FileNames;
        if (dllPaths.Length == 0) return;

        var assemblies = new List<Assembly>();
        foreach (var dllPath in dllPaths)
        {
            try
            {
                assemblies.Add(Assembly.LoadFrom(dllPath));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Warning: Failed to load {Path.GetFileName(dllPath)}: {ex.Message}";
            }
        }

        if (assemblies.Count == 0)
        {
            StatusMessage = "No assemblies could be loaded.";
            return;
        }

        await RunStubGenerationAsync(assemblies.ToArray()).ConfigureAwait(true);
    }

    private async Task RunStubGenerationAsync(Assembly[] assemblies)
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            StatusMessage = "Please specify an output path.";
            return;
        }

        IsGenerating = true;
        StatusMessage = "Generating stubs...";
        Progress = 0;
        GenerateStubsCommand.NotifyCanExecuteChanged();
        GenerateExternalStubsCommand.NotifyCanExecuteChanged();

        try
        {
            var destPath = new DirectoryInfo(OutputPath);

            await Task.Run(() =>
            {
                StubBuilder.BuildAssemblyStubs(
                    destPath,
                    assemblies,
                    logger: message => DispatcherHelper.RunOnMainThread(() => StatusMessage = message));
            }).ConfigureAwait(true);

            StatusMessage = $"Stubs generated successfully at: {OutputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
            Progress = 100;
            GenerateStubsCommand.NotifyCanExecuteChanged();
            GenerateExternalStubsCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanGenerate() => !IsGenerating;

    [RelayCommand]
    private void Cancel()
    {
        CloseAction?.Invoke();
    }
}

/// <summary>
/// Represents a .NET assembly selectable for stub generation.
/// </summary>
public partial class AssemblyItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Short assembly name (e.g. "RevitAPI").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full assembly name including version and culture.
    /// </summary>
    public required string FullName { get; init; }

    /// <summary>
    /// File path of the assembly on disk.
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// Reference to the loaded assembly instance.
    /// Null for custom DLLs that haven't been loaded yet.
    /// </summary>
    public Assembly? Assembly { get; init; }
}
