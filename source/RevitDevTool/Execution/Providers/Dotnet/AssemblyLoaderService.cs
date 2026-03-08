using System.Diagnostics;
using System.IO;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
namespace RevitDevTool.Execution.Providers.Dotnet;

/// <summary>
/// Service for loading Revit add-in assemblies with automatic dependency resolution.
/// Uses MetadataLoadContext for metadata-only inspection to avoid AppDomain type identity issues.
/// </summary>
public static class AssemblyLoaderService
{
    private static readonly string CommandFullName = typeof(IExternalCommand).FullName!;
    private static readonly string TransactionAttributeFullName = typeof(TransactionAttribute).FullName!;

    /// <summary>
    /// Parses IExternalCommand implementations from a given assembly file using MetadataLoadContext
    /// </summary>
    /// <param name="originalFilePath">Original file path of the assembly</param>
    /// <returns>List of AddinItem representing commands found</returns>
    public static List<AddinItem> ParseCommands(string originalFilePath)
    {
        if (!File.Exists(originalFilePath))
        {
            Trace.TraceError($"Assembly file not found: {originalFilePath}");
            return [];
        }

        var commands = new List<AddinItem>();

        try
        {
            var paths = CollectAssemblyPaths(originalFilePath);
            var resolver = new PathAssemblyResolver(paths);
            using var mlc = new MetadataLoadContext(resolver);

            var assembly = mlc.LoadFromAssemblyPath(originalFilePath);
            var revitApiAssembly = typeof(IExternalCommand).Assembly;
            var revitApiInContext = mlc.LoadFromAssemblyPath(revitApiAssembly.Location);
            var iExternalCommandType = revitApiInContext.GetType(CommandFullName);

            if (iExternalCommandType == null)
            {
                Trace.TraceError($"Could not find {CommandFullName} in metadata context");
                return commands;
            }

            foreach (var type in GetMetadataTypes(assembly))
            {
                var item = TryParseCommandType(type, iExternalCommandType, originalFilePath);
                if (item != null)
                {
                    commands.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Failed to parse commands from {originalFilePath}: {ex.Message}");
        }

        return commands;
    }

    private static List<Type> GetMetadataTypes(Assembly assembly)
    {
        var types = new List<Type>();

        try
        {
            types.AddRange(assembly.GetTypes());
        }
        catch (ReflectionTypeLoadException ex)
        {
            // ReSharper disable once RedundantSuppressNullableWarningExpression
            types.AddRange(ex.Types.Where(t => t != null)!);
        }
        catch
        {
            types.AddRange(GetTypesIndividually(assembly));
        }

        return types;
    }

    private static List<Type> GetTypesIndividually(Assembly assembly)
    {
        var types = new List<Type>();

        foreach (var typeInfo in assembly.DefinedTypes)
        {
            try
            {
                var type = typeInfo.AsType();
                _ = type.BaseType;
                types.Add(type);
            }
            catch
            {
                // Skip types that fail to load
            }
        }

        return types;
    }

    /// <summary>
    /// Collects all assembly paths needed for MetadataLoadContext resolution
    /// </summary>
    public static List<string> CollectAssemblyPaths(string targetAssemblyPath)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Target assembly directory
        var targetDir = Path.GetDirectoryName(targetAssemblyPath);
        if (!string.IsNullOrEmpty(targetDir))
            AddDllsFromDirectory(paths, targetDir);

        // 2. Revit API directory
        var revitApiPath = typeof(IExternalCommand).Assembly.Location;
        var revitApiDir = Path.GetDirectoryName(revitApiPath);
        if (!string.IsNullOrEmpty(revitApiDir))
            AddDllsFromDirectory(paths, revitApiDir);

        // 3. Framework directory (includes mscorlib, System.dll, etc.)
        var frameworkDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (!string.IsNullOrEmpty(frameworkDir))
            AddDllsFromDirectory(paths, frameworkDir);

        // 4. WPF assemblies
        var presentationFrameworkPath = typeof(System.Windows.Window).Assembly.Location;
        var wpfDir = Path.GetDirectoryName(presentationFrameworkPath);
        if (!string.IsNullOrEmpty(wpfDir))
            AddDllsFromDirectory(paths, wpfDir);

        // 5. WPF Media assemblies
        var presentationCorePath = typeof(System.Windows.Media.Visual).Assembly.Location;
        var wpfCoreDir = Path.GetDirectoryName(presentationCorePath);
        if (!string.IsNullOrEmpty(wpfCoreDir))
            AddDllsFromDirectory(paths, wpfCoreDir);

        // 6. WindowsBase assembly
        var windowsBasePath = typeof(System.Windows.DependencyObject).Assembly.Location;
        var windowsBaseDir = Path.GetDirectoryName(windowsBasePath);
        if (!string.IsNullOrEmpty(windowsBaseDir))
            AddDllsFromDirectory(paths, windowsBaseDir);
        
        // 7. Current executing assembly
        var currentAssemblyPath = Assembly.GetExecutingAssembly().Location;
        var currentAssemblyDir = Path.GetDirectoryName(currentAssemblyPath);
        if (!string.IsNullOrEmpty(currentAssemblyDir))
            AddDllsFromDirectory(paths, currentAssemblyDir);

        return paths.ToList();
    }

    private static void AddDllsFromDirectory(HashSet<string> paths, string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return;
            foreach (var dll in Directory.GetFiles(directory, "*.dll"))
                paths.Add(dll);
        }
        catch
        {
            // Ignore errors scanning directories
        }
    }

    /// <summary>
    /// Attempts to parse a command type using MetadataLoadContext and create an AddinItem
    /// </summary>
    private static AddinItem? TryParseCommandType(Type type, Type iExternalCommandType, string originalFilePath)
    {
        try
        {
            if (type.IsAbstract || type.IsInterface)
                return null;

            var implementsInterface = type.GetInterfaces().Any(i => i.FullName == iExternalCommandType.FullName);

            if (!implementsInterface)
                return null;

            var transactionMode = ExtractAttributes(type);

            if (transactionMode != null)
            {
                return new AddinItem(originalFilePath, type.FullName ?? string.Empty);
            }

            Trace.TraceWarning($"{type.FullName} implements IExternalCommand but missing TransactionAttribute");
            return null;
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error parsing type {type.FullName}: {ex.Message}");
            return null;
        }
    }

    private static TransactionMode? ExtractAttributes(Type type)
    {
        TransactionMode? transactionMode = null;

        foreach (var attrData in type.GetCustomAttributesData())
        {
            var attrTypeName = attrData.AttributeType.FullName;
            var firstArg = attrData.ConstructorArguments.FirstOrDefault().Value;

            if (attrTypeName == TransactionAttributeFullName && firstArg is int transVal)
                transactionMode = (TransactionMode) transVal;
        }

        return transactionMode;
    }
}
