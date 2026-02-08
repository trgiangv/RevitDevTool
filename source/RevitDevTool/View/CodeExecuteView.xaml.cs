using System.Diagnostics;
using System.IO;
using CodeExecuteViewModel = RevitDevTool.ViewModel.CodeExecuteViewModel;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;

namespace RevitDevTool.View;

public partial class CodeExecuteView
{
    public CodeExecuteView(CodeExecuteViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        // Defer loading until after XAML parsing completes
        // This prevents race condition with MetroTabControl's CloseTabItemAction
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, viewModel.LoadSavedPathsAsync);
    }

    private async void AddinTreeView_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!TryGetDroppedFiles(e, out var files) || DataContext is not CodeExecuteViewModel viewModel)
                return;

            foreach (var filePath in files)
            {
                await ProcessDroppedItemAsync(filePath, viewModel).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error handling drop event: {ex.Message}");
        }
    }

    private static bool TryGetDroppedFiles(DragEventArgs e, out string[] files)
    {
        files = [];

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;

        var droppedObject = e.Data.GetData(DataFormats.FileDrop);
        if (droppedObject is not string[] droppedFiles || droppedFiles.Length == 0)
            return false;

        files = droppedFiles;
        return true;
    }

    private static async Task ProcessDroppedItemAsync(string filePath, CodeExecuteViewModel viewModel)
    {
        if (File.Exists(filePath))
        {
            await ProcessDroppedFileAsync(filePath, viewModel).ConfigureAwait(false);
        }
        else if (Directory.Exists(filePath))
        {
            await viewModel.LoadFromPathAsync(filePath).ConfigureAwait(false);
        }
    }

    private static async Task ProcessDroppedFileAsync(string filePath, CodeExecuteViewModel viewModel)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        switch (ext)
        {
            case ".dll":
                await ProcessDllFileAsync(filePath, viewModel).ConfigureAwait(false);
                break;
            case ".py":
                await ProcessPythonFileAsync(filePath, viewModel).ConfigureAwait(false);
                break;
        }
    }

    private static async Task ProcessDllFileAsync(string filePath, CodeExecuteViewModel viewModel)
    {
        if (Utils.AssemblyLoader.IsManagedAssembly(filePath))
        {
            await viewModel.LoadFromPathAsync(filePath).ConfigureAwait(false);
        }
        else
        {
            Trace.TraceWarning($"File {filePath} is not a valid managed assembly.");
        }
    }

    private static async Task ProcessPythonFileAsync(string filePath, CodeExecuteViewModel viewModel)
    {
        var folderPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(folderPath))
        {
            await viewModel.LoadFromPathAsync(folderPath).ConfigureAwait(false);
        }
    }
}