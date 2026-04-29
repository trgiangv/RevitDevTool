using System.Reflection;
namespace DevTools.Presentation.Models;

public partial class AssemblyItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string Location { get; init; }
    public Assembly? Assembly { get; init; }
}