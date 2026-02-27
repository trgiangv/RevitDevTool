using Avalonia.Controls;
using Avalonia.Controls.Templates;
using RevitDevTool.Desktop.ViewModels;

namespace RevitDevTool.Desktop.Common;

/// <summary>
/// Resolves a View for a given ViewModel by convention:
/// replaces "ViewModel" suffix with "View" and looks in the Views.Pages namespace.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    public static readonly ViewLocator Instance = new();

    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var vmTypeName = param.GetType().FullName;
        if (vmTypeName is null)
            return new TextBlock { Text = "ViewModel type name is null" };

        var viewTypeName = vmTypeName
            .Replace(".ViewModels.", ".Views.Pages.")
            .Replace("ViewModel", "View");

        var viewType = Type.GetType(viewTypeName);
        if (viewType is null)
            return new TextBlock { Text = $"View not found: {viewTypeName}" };

        return (Control)Activator.CreateInstance(viewType)!;
    }

    public bool Match(object? data) => data is PageViewModelBase;
}
