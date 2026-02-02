using System.Diagnostics;
using RevitDevTool.ViewModel.Execute;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;

namespace RevitDevTool.View;

public partial class CodeExecuteView
{
    public CodeExecuteView(CodeExecuteViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void AddinTreeView_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not CodeExecuteViewModel coordinator) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var droppedObject = e.Data.GetData(DataFormats.FileDrop);
        if (droppedObject is not string[] files || files.Length == 0) return;

        foreach (var filePath in files)
        {
            try
            {
                // Only handle CSharp mode for now - delegate to active ViewModel
                if (coordinator.ActiveViewModel is CSharpExecuteViewModel csharpVm)
                {
                    ParseAssembly(filePath, csharpVm);
                }
                // TODO: Handle Python .py file drops when in Python mode
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to load file from {filePath}: {ex.Message}");
            }
        }
    }

    private static void ParseAssembly(string filePath, CSharpExecuteViewModel viewModel)
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