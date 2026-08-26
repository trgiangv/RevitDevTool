#if !REVIT2025_OR_GREATER
using CSharpDemo.Ceilings;
// ReSharper disable once CheckNamespace
namespace Autodesk.Revit.DB;

[PublicAPI]
public static class CeilingExtensions
{
    extension(Ceiling ceiling)
    {
        public IList<Curve> GetCeilingGridLines(bool includeBoundary)
        {
            if (ceiling is null)
                throw new ArgumentNullException(nameof(ceiling));

            return CeilingGridLines.Get(ceiling, includeBoundary);
        }

        public IList<Curve> GetCeilingGridLines(
            RevitLinkInstance linkInstance,
            bool includeBoundary)
        {
            if (ceiling is null)
                throw new ArgumentNullException(nameof(ceiling));
            if (linkInstance is null)
                throw new ArgumentNullException(nameof(linkInstance));

            return CeilingGridLines.Get(ceiling, includeBoundary, linkInstance);
        }
    }
}
#endif
