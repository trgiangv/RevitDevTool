using RevitDevTool.Theme ;
using ApplicationTheme = UIFramework.ApplicationTheme;

namespace RevitDevTool.View ;

public partial class Settings
{
  public Settings()
  {
    InitializeComponent() ;
#if REVIT2024_OR_GREATER
    ApplicationTheme.CurrentTheme.PropertyChanged += ThemeWatcher.ApplyTheme;
#endif
    ThemeWatcher.Instance.Watch(this);
    ThemeWatcher.Instance.ApplyTheme();
  }

}