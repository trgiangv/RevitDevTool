using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows.Data;
using PythonNetStubGenerator;
using RevitDevTool.Utils;
// ReSharper disable UnusedParameterInPartialMethod

namespace RevitDevTool.ViewModel;

public partial class StubBuilderViewModel : ObservableObject
{
    [ObservableProperty]
    private string _outputPath;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _hideUnchecked;

    private ObservableCollection<AssemblyItem> AppDomainAssemblies { get; } = [];

    public ICollectionView FilteredAssemblies { get; }

    public Action? CloseAction { get; set; }

    public StubBuilderViewModel()
    {
        _outputPath = Path.Combine(SettingsUtils.GetContentRootPath(), "Stubs");

        FilteredAssemblies = CollectionViewSource.GetDefaultView(AppDomainAssemblies);
        FilteredAssemblies.Filter = FilterAssembly;

        LoadAppDomainAssemblies();
    }

    partial void OnHideUncheckedChanged(bool value) => FilteredAssemblies.Refresh();

    private bool FilterAssembly(object obj)
    {
        if (obj is not AssemblyItem item) return false;
        return !HideUnchecked || item.IsSelected;
    }

    private void LoadAppDomainAssemblies()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
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
            item.IsSelected = true;
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

        try
        {
            var destPath = new DirectoryInfo(OutputPath);

            await Task.Run(() =>
            {
                StubBuilder.BuildAssemblyStubs(destPath, assemblies);
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
