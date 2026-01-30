using System.Diagnostics;
using RevitDevTool.ViewModel;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;

namespace RevitDevTool.View;

public partial class AddinLoadView
{
    public AddinLoadView(AddinLoadViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void AddinTreeView_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not AddinLoadViewModel viewModel) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var droppedObject = e.Data.GetData(DataFormats.FileDrop);
        if (droppedObject is not string[] files || files.Length == 0) return;

        foreach (var filePath in files)
        {
            try 
            {
                ParseAssembly(filePath, viewModel);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to load assembly from {filePath}: {ex.Message}");
            }
        }
    }

    private static void ParseAssembly(string filePath, AddinLoadViewModel viewModel)
    {
        if (Utils.AssemblyLoader.IsManagedAssembly(filePath))
        {
            viewModel.LoadAssembly(filePath);
        }
        else
        {
            Trace.TraceWarning($"File {filePath} is not a valid managed assembly.");
        }
    }
}