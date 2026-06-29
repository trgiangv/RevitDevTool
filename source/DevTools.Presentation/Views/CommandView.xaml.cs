using System.IO;
using System.Windows;
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

    private void AddinTreeView_DragEnter(object sender, DragEventArgs e)
    {
        if (!TryGetPaths(e, out var paths) || DataContext is not CommandViewModel vm) return;
        var valid = paths.Length > 0 && paths.All(IsSupportedPath);
        e.Effects = valid ? DragDropEffects.Copy : DragDropEffects.None;
        UpdateDropMaskText(valid);
        vm.ShowDropMask();
    }

    private void AddinTreeView_DragOver(object sender, DragEventArgs e)
    {
        if (TryGetPaths(e, out var paths) && DataContext is CommandViewModel vm)
        {
            var valid = paths.Length > 0 && paths.All(IsSupportedPath);
            e.Effects = valid ? DragDropEffects.Copy : DragDropEffects.None;
            UpdateDropMaskText(valid);
            vm.ShowDropMask();
        }
        e.Handled = true;
    }

    private void AddinTreeView_DragLeave(object sender, DragEventArgs e)
    {
        if (DataContext is CommandViewModel vm) vm.IsDropMaskVisible = false;
    }

    private async void AddinTreeView_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (TryGetPaths(e, out var paths) && DataContext is CommandViewModel vm)
                await vm.HandleDropAsync(paths, IsSupportedPath);
        }
        catch
        {
            // Ignored
        }
    }

    private void UpdateDropMaskText(bool isValid)
    {
        DropMaskTitle.Text = isValid ? ValidDropTitle : InvalidDropTitle;
        DropMaskHint.Text = isValid ? ValidDropHint : InvalidDropHint;
    }

    private static bool IsSupportedPath(string path)
    {
        if (Directory.Exists(path)) return true;
        return File.Exists(path) && string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetPaths(DragEventArgs e, out string[] paths)
    {
        paths = [];
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return false;
        paths = files;
        return true;
    }
}
