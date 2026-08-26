using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace AcadDevTool.HostAdapters;

internal static class AcadHostApis
{
    internal static IEnumerable<Assembly> All()
    {
        yield return typeof(CommandMethodAttribute).Assembly;
        yield return typeof(Database).Assembly;
        yield return typeof(Autodesk.AutoCAD.ApplicationServices.Core.Application).Assembly;
    }
}
