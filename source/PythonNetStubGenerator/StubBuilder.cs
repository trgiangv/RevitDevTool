using System.Reflection;

namespace PythonNetStubGenerator;

public static class StubBuilder
{
    private static HashSet<DirectoryInfo> SearchPaths { get; } = [];

    /// <summary>
    /// Build stubs from pre-loaded assemblies (e.g. from AppDomain.CurrentDomain).
    /// Skips Assembly.LoadFrom since assemblies are already loaded in memory.
    /// </summary>
    public static void BuildAssemblyStubs(DirectoryInfo destPath, Assembly[] targetAssemblies, DirectoryInfo[]? searchPaths = null, Action<string>? logger = null)
    {
        PrepareResolver();
        AddSearchPaths(searchPaths);

        // Build doc provider: XML doc priority, reflection fallback
        var docProvider = new XmlDocProvider(Log);
        Log("Loading XML documentation...");

        foreach (var assembly in targetAssemblies)
        {
            try
            {
                AddSearchPathFromAssembly(assembly);

                // Load XML documentation for this assembly
                docProvider.AddAssembly(assembly);

                Log($"Generating Assembly: {assembly.FullName}");
                AddExportedTypes(assembly);
            }
            catch (Exception ex)
            {
                Log($"Warning: Failed to process assembly {assembly.FullName}: {ex.Message}");
            }
        }

        Log($"XML documentation loaded for {docProvider.LoadedXmlCount} assemblies.");

        GenerateStubs(destPath, docProvider);
        return;

        void Log(string message) => (logger ?? Console.WriteLine)(message);
    }

    private static void PrepareResolver()
    {
        AppDomain.CurrentDomain.AssemblyResolve -= AssemblyResolve;
        AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
    }

    private static void AddSearchPaths(DirectoryInfo[]? searchPaths)
    {
        if (searchPaths == null) return;
        foreach (var path in searchPaths)
            SearchPaths.Add(path);
    }

    private static void AddSearchPathFromAssembly(Assembly assembly)
    {
        if (string.IsNullOrEmpty(assembly.Location)) return;

        var directoryPath = Path.GetDirectoryName(assembly.Location);
        if (string.IsNullOrEmpty(directoryPath)) return;
        SearchPaths.Add(new DirectoryInfo(directoryPath));
    }

    private static void AddExportedTypes(Assembly assembly)
    {
        foreach (var exportedType in assembly.GetExportedTypes())
        {
            if (!exportedType.IsVisible) continue;
            PythonTypes.AddDependency(exportedType);
        }
    }

    private static void GenerateStubs(DirectoryInfo destPath, XmlDocProvider docProvider)
    {
        StubWriter.DocProvider = docProvider;
        try
        {
            GenerateBuiltInAndWrite(destPath);
        }
        finally
        {
            StubWriter.DocProvider = null;
        }
    }

    private static void GenerateBuiltInAndWrite(DirectoryInfo destPath)
    {
        var typeAssembly = typeof(Type).Assembly;
        Console.WriteLine($"Generating Built-in Assembly: {typeAssembly.FullName}");

        foreach (var exportedType in typeAssembly.GetExportedTypes())
        {
            if(!exportedType.IsVisible) continue;
            PythonTypes.AddDependency(exportedType);
        }

        var consoleAssembly = typeof(Console).Assembly;
        Console.WriteLine($"Generating Built-in Assembly: {consoleAssembly.FullName}");
        foreach (var exportedType in consoleAssembly.GetExportedTypes())
        {
            if(!exportedType.IsVisible) continue;
            PythonTypes.AddDependency(exportedType);
        }

        while (true)
        {
            var (nameSpace, types) = PythonTypes.RemoveDirtyNamespace();
            if (nameSpace == null) break;

            // generate stubs for each type
            WriteStub(destPath, nameSpace, types);
        }
    }

    private static void WriteStub(DirectoryInfo rootDirectory, string nameSpace, IEnumerable<Type> stubTypes)
    {
        // sort the stub list so we get consistent output over time
        var orderedTypes = stubTypes.OrderBy(it => it.Name);

        var path = nameSpace.Split('.').Aggregate(rootDirectory.FullName, Path.Combine);

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        path = Path.Combine(path, "__init__.pyi");

        PythonTypes.ClearCurrent();

        var stubText = StubWriter.GetStub(nameSpace, orderedTypes);


        File.WriteAllText(path, stubText);
    }


    private static Assembly? AssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var parts = args.Name.Split(',');

        var assemblyToResolve = $"{parts[0]}.dll";

        // try to find the dll in given search paths
        foreach (var searchPath in SearchPaths)
        {
            var assemblyPath = Path.Combine(searchPath.FullName, assemblyToResolve);
            if (File.Exists(assemblyPath)) return Assembly.LoadFrom(assemblyPath);
        }

        return null;
    }
}
