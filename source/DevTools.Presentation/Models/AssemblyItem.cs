using System.Reflection;
namespace DevTools.Presentation.Models;

public partial class AssemblyItem : ObservableObject
{
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string Location { get; init; }
    public Assembly? Assembly { get; init; }
}