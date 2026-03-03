using System.Diagnostics;
using System.IO;
using RevitDevTool.ViewModel;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;

namespace RevitDevTool.View;

public partial class ExecutionView
{
    private const string ValidDropTitle = "Drop to load";
    private const string ValidDropHint = ".dll files and folders are supported";
    private const string InvalidDropTitle = "Unsupported drop";
    private const string InvalidDropHint = "Only .dll files and folders are supported";

    public ExecutionView(ExecutionViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        // Defer loading until after XAML parsing completes
        // This prevents race condition with MetroTabControl's CloseTabItemAction
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, viewModel.LoadSavedPathsAsync);
    }

    private void AddinTreeView_DragEnter(object sender, DragEventArgs e)
    {
        UpdateDropMaskState(e);
    }

    private void AddinTreeView_DragOver(object sender, DragEventArgs e)
    {
        UpdateDropMaskState(e);
        e.Handled = true;
    }

    private void AddinTreeView_DragLeave(object sender, DragEventArgs e)
    {
        HideDropMask();
    }

    private async void AddinTreeView_Drop(object sender, DragEventArgs e)
    {
        try
        {
            HideDropMask();

            if (!TryGetDroppedPaths(e, out var droppedPaths) || DataContext is not ExecutionViewModel viewModel)
                return;

            foreach (var droppedPath in droppedPaths)
            {
                await ProcessDroppedItemAsync(droppedPath, viewModel);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error handling drop event: {ex.Message}");
        }
    }

    private static bool TryGetDroppedPaths(DragEventArgs e, out string[] paths)
    {
        paths = [];

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;

        var droppedObject = e.Data.GetData(DataFormats.FileDrop);
        if (droppedObject is not string[] droppedFiles || droppedFiles.Length == 0)
            return false;

        paths = droppedFiles;
        return true;
    }

    private void UpdateDropMaskState(DragEventArgs e)
    {
        var isValid = IsValidDropData(e);
        e.Effects = isValid ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;

        DropMaskTitle.Text = isValid ? ValidDropTitle : InvalidDropTitle;
        DropMaskHint.Text = isValid ? ValidDropHint : InvalidDropHint;
        DropMask.Visibility = System.Windows.Visibility.Visible;
    }

    private static bool IsValidDropData(DragEventArgs e)
    {
        return TryGetDroppedPaths(e, out var droppedPaths) && droppedPaths.All(IsSupportedPath);
    }

    private static bool IsSupportedPath(string path)
    {
        if (Directory.Exists(path))
            return true;

        return File.Exists(path) && string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);
    }

    private void HideDropMask()
    {
        DropMask.Visibility = System.Windows.Visibility.Collapsed;
    }

    private static async Task ProcessDroppedItemAsync(string path, ExecutionViewModel viewModel)
    {
        if (!IsSupportedPath(path))
        {
            Trace.TraceWarning($"Unsupported drop item: {path}. Only .dll files and folders are supported.");
            return;
        }

        if (File.Exists(path))
        {
            await ProcessDroppedDllFileAsync(path, viewModel);
        }
        else if (Directory.Exists(path))
        {
            await viewModel.LoadFromPathAsync(path);
        }
    }

    private static async Task ProcessDroppedDllFileAsync(string filePath, ExecutionViewModel viewModel)
    {
        if (Utils.AssemblyLoader.IsManagedAssembly(filePath))
        {
            await viewModel.LoadFromPathAsync(filePath);
        }
        else
        {
            Trace.TraceWarning($"File {filePath} is not a valid managed assembly.");
        }
    }
}