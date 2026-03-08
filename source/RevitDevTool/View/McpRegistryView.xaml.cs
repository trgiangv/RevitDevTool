using System.Diagnostics;
using System.IO;
using RevitDevTool.ViewModel;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;

namespace RevitDevTool.View;

public partial class McpRegistryView
{
    private const string PythonToolPattern = "*mcp.py";
    private const string ValidDropTitle = "Drop to load";
    private const string ValidDropHint = "Direct .dll files or Python toolset folders are supported";
    private const string InvalidDropTitle = "Unsupported drop";
    private const string InvalidDropHint = "Only direct .dll files or Python toolset folders are supported";

    public McpRegistryView(McpRegistryViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            viewModel.InitializeAsync);
    }

    private void RegistryList_DragEnter(object sender, DragEventArgs e)
    {
        UpdateDropMaskState(e);
    }

    private void RegistryList_DragOver(object sender, DragEventArgs e)
    {
        UpdateDropMaskState(e);
        e.Handled = true;
    }

    private void RegistryList_DragLeave(object sender, DragEventArgs e)
    {
        HideDropMask();
    }

    private async void RegistryList_Drop(object sender, DragEventArgs e)
    {
        try
        {
            HideDropMask();
            if (!TryGetDroppedPaths(e, out var droppedPaths) || DataContext is not McpRegistryViewModel viewModel)
                return;

            foreach (var path in droppedPaths)
            {
                await viewModel.AddDroppedPathAsync(path);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error handling MCP drop event: {ex.Message}");
        }
    }

    private static bool TryGetDroppedPaths(DragEventArgs e, out string[] paths)
    {
        paths = [];
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] dropped || dropped.Length == 0)
            return false;

        paths = dropped;
        return true;
    }

    private static bool IsSupportedPath(string path)
    {
        if (Directory.Exists(path))
            return Directory.EnumerateFiles(path, PythonToolPattern, SearchOption.AllDirectories).Any();

        return File.Exists(path) && string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidDropData(DragEventArgs e)
    {
        return TryGetDroppedPaths(e, out var droppedPaths) && droppedPaths.All(IsSupportedPath);
    }

    private void UpdateDropMaskState(DragEventArgs e)
    {
        var isValid = IsValidDropData(e);
        e.Effects = isValid ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        DropMaskTitle.Text = isValid ? ValidDropTitle : InvalidDropTitle;
        DropMaskHint.Text = isValid ? ValidDropHint : InvalidDropHint;
        DropMask.Visibility = System.Windows.Visibility.Visible;
    }

    private void HideDropMask()
    {
        DropMask.Visibility = System.Windows.Visibility.Collapsed;
    }
}