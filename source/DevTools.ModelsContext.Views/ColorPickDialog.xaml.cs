using System.Windows;
using System.Windows.Media;

namespace DevTools.ModelsContext.Views;

public partial class ColorPickDialog : Window
{
    public ColorPickDialog()
    {
        InitializeComponent();
    }

    public Color? Picked { get; private set; }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Picked = Picker.SelectedColor;
        DialogResult = true;
    }
}
