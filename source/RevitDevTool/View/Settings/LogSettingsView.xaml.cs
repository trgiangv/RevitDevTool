using System.Windows.Controls;
using RevitDevTool.Theme;
using RevitDevTool.ViewModel.Settings;
using Wpf.Ui.Controls;

namespace RevitDevTool.View.Settings;

public sealed partial class LogSettingsView
{
    private readonly LogSettingsViewModel _viewModel;

    public LogSettingsView(ContentPresenter dialogHost) : base(dialogHost)
    {
        _viewModel = LogSettingsViewModel.Instance;
        DataContext = _viewModel;
        
        InitializeComponent();
        ThemeWatcher.Instance.Watch(this);
        ButtonClicked += OnButtonClicked;
    }

    private void OnButtonClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (args.Button == ContentDialogButton.Primary)
        {
            _viewModel.SaveToConfig();
        }
    }
}