using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;

namespace RevitDevTool.Desktop.ViewModels;

public partial class DataPageViewModel : PageViewModelBase
{
    public override int Index => 2;
    public override string DisplayName => "Data";
    public override MaterialIconKind Icon => MaterialIconKind.Database;

    [ObservableProperty] private string _ragStatus = "Not Configured";
    [ObservableProperty] private bool _isRagEnabled;
    [ObservableProperty] private string _ragEndpoint = "http://localhost:11434";
    [ObservableProperty] private string _ragModel = "nomic-embed-text";
    [ObservableProperty] private int _indexedDocumentsCount;
    [ObservableProperty] private bool _isIndexing;
    [ObservableProperty] private string _indexingProgress = string.Empty;

    // RAG Setup properties
    [ObservableProperty] private string _selectedEmbeddingModel = "nomic-embed-text";
    [ObservableProperty] private decimal _chunkSize = 512;
    [ObservableProperty] private decimal _chunkOverlap = 64;
    [ObservableProperty] private string _dataDirectory = string.Empty;
    [ObservableProperty] private bool _hasIndex;
    [ObservableProperty] private string _indexStatus = "No Index";

    public IReadOnlyList<string> EmbeddingModels { get; } = ["nomic-embed-text", "BAAI/bge-small-en-v1.5", "sentence-transformers/all-MiniLM-L6-v2"];
    public ObservableCollection<IndexedDocument> IndexedDocuments { get; } = [];
    public ObservableCollection<CustomDataSourceConfig> CustomDataSources { get; } = [];

    public DataPageViewModel()
    {
        CustomDataSources.Add(new CustomDataSourceConfig { Name = "Sample Source", Type = "FileSystem", IsEnabled = true });
    }

    [RelayCommand]
    private void AddDataSource()
    {
        CustomDataSources.Add(new CustomDataSourceConfig { Name = "New Source" });
    }

    [RelayCommand]
    private void RemoveDataSource(CustomDataSourceConfig? source)
    {
        if (source != null)
            CustomDataSources.Remove(source);
    }

    [RelayCommand]
    private void BrowseDataDirectory()
    {
        // Open folder picker
    }

    [RelayCommand]
    private void SaveDataSources()
    {
        IndexingProgress = "Data sources saved successfully!";
    }

    [RelayCommand]
    private void BuildIndex()
    {
        HasIndex = true;
        IndexStatus = "Building...";
        _ = StartIndexingAsync();
    }

    [RelayCommand]
    private void ClearIndex()
    {
        IndexedDocuments.Clear();
        IndexedDocumentsCount = 0;
        IndexingProgress = "Index cleared.";
        RagStatus = "Not Configured";
        HasIndex = false;
        IndexStatus = "No Index";
    }

    private async Task StartIndexingAsync()
    {
        IsIndexing = true;
        IndexingProgress = "Starting indexing...";

        try
        {
            for (int i = 0; i <= 100; i += 10)
            {
                await Task.Delay(200);
                IndexingProgress = $"Indexing... {i}%";
                IndexedDocumentsCount = i;
            }
            IndexingProgress = "Indexing completed!";
            RagStatus = "Ready";
            IndexStatus = "Ready";
        }
        catch (Exception ex)
        {
            IndexingProgress = $"Indexing failed: {ex.Message}";
            RagStatus = "Error";
            IndexStatus = "Error";
        }
        finally
        {
            IsIndexing = false;
        }
    }
}

public partial class IndexedDocument : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _path = string.Empty;
    [ObservableProperty] private DateTime _indexedAt;
    [ObservableProperty] private int _chunkCount;
}

public partial class CustomDataSourceConfig : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _type = string.Empty;
    [ObservableProperty] private string _connectionString = string.Empty;
    [ObservableProperty] private bool _isEnabled = true;
}
