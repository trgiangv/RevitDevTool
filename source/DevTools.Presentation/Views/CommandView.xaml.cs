using System.Diagnostics;
using System.IO;
using DevTools.Presentation.ViewModels;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;

namespace DevTools.Presentation.Views;

public partial class CommandView
{
    private const string ValidDropTitle = "Drop to load";
    private const string ValidDropHint = ".dll files and folders are supported";
    private const string InvalidDropTitle = "Unsupported drop";
    private const string InvalidDropHint = "Only .dll files and folders are supported";

    public CommandView()
    {
        InitializeComponent();
    }

    private void AddinTreeView_DragEnter(object sender, DragEventArgs e) => UpdateDropMaskState(e);

    private void AddinTreeView_DragOver(object sender, DragEventArgs e)
    {
        UpdateDropMaskState(e);
        e.Handled = true;
    }

    private void AddinTreeView_DragLeave(object sender, DragEventArgs e) => HideDropMask();

    private async void AddinTreeView_Drop(object sender, DragEventArgs e)
    {
        try
        {
            HideDropMask();
            if (!TryGetDroppedPaths(e, out var droppedPaths) || DataContext is not CommandViewModel viewModel) return;
            foreach (var droppedPath in droppedPaths)
                await ProcessDroppedItemAsync(droppedPath, viewModel);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error handling drop event: {ex.Message}");
        }
    }

    private static bool TryGetDroppedPaths(DragEventArgs e, out string[] paths)
    {
        paths = [];
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] droppedFiles || droppedFiles.Length == 0) return false;
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
        if (Directory.Exists(path)) return true;
        return File.Exists(path) && string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);
    }

    private void HideDropMask() => DropMask.Visibility = System.Windows.Visibility.Collapsed;

    private static async Task ProcessDroppedItemAsync(string path, CommandViewModel viewModel)
    {
        if (!IsSupportedPath(path))
        {
            Trace.TraceWarning($"Unsupported drop item: {path}. Only .dll files and folders are supported.");
            return;
        }
        if (File.Exists(path))
            await ProcessDroppedDllFileAsync(path, viewModel);
        else if (Directory.Exists(path))
            await viewModel.LoadFromPathAsync(path);
    }

    private static async Task ProcessDroppedDllFileAsync(string filePath, CommandViewModel viewModel)
    {
        if (Utilities.AssemblyLoader.IsManagedAssembly(filePath))
            await viewModel.LoadFromPathAsync(filePath);
        else
            Trace.TraceWarning($"File {filePath} is not a valid managed assembly.");
    }
}
