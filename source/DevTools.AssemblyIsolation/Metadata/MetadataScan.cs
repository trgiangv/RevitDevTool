using System.Reflection;

namespace DevTools.AssemblyIsolation.Metadata;

public sealed class MetadataScan
{
    private readonly List<string> _skipped = [];

    public IReadOnlyList<string> Skipped => _skipped;

    public IReadOnlyList<Type> GetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (var loaderException in ex.LoaderExceptions.OfType<Exception>())
                _skipped.Add($"{assembly.GetName().Name}: {loaderException.Message}");
            return ex.Types.OfType<Type>().ToArray();
        }
    }

    public bool TryRead<T>(string member, Func<T> read, out T value)
    {
        try
        {
            value = read();
            return true;
        }
        catch (Exception ex) when (IsUnresolvedReference(ex))
        {
            _skipped.Add($"{member}: {ex.Message}");
            value = default!;
            return false;
        }
    }

    public static CustomAttributeData? FindAttribute(MemberInfo member, string attributeFullName)
    {
        foreach (var data in member.GetCustomAttributesData())
        {
            try
            {
                if (data.AttributeType.FullName == attributeFullName)
                    return data;
            }
            catch (Exception ex) when (IsUnresolvedReference(ex))
            {
            }
        }

        return null;
    }

    private static bool IsUnresolvedReference(Exception ex) =>
        ex is FileNotFoundException or FileLoadException or TypeLoadException;
}
