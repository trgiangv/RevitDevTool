using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RevitDevTool.Desktop.ViewModels;

namespace RevitDevTool.Desktop.Views.Processor;

public partial class ExecutionLogicPaneView : UserControl
{
    public ExecutionLogicPaneView()
    {
        InitializeComponent();
    }

    private async void OnBrowseConfigClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProcessorPageViewModel vm)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Select batch config",
            FileTypeFilter =
            [
                new FilePickerFileType("JSON")
                {
                    Patterns = ["*.json"]
                }
            ]
        });

        var file = files.FirstOrDefault();
        if (file != null)
        {
            vm.ConfigPath = file.Path.LocalPath;
            if (vm.LoadPlanCommand.CanExecute(null))
                await vm.LoadPlanCommand.ExecuteAsync(null);
        }
    }
}
