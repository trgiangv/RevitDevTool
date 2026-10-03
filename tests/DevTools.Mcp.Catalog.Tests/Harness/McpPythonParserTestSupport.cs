using DevTools.Execution.Providers.Python;
using Python.Runtime;

namespace DevTools.Mcp.Catalog.Tests.Harness;

internal static class McpPythonParserTestSupport
{
    private static readonly Lock InitLock = new();
    private static bool _initialized;

    public static void RequirePythonRuntime()
    {
        if (!TryGetPythonHome(out _, out _))
            Assert.Inconclusive(OptionalArtifact.PixiPythonHint);

        OptionalArtifact.RequireFile(GetToolParserScriptPath(), $"ToolParser.py not found at '{GetToolParserScriptPath()}'.");
    }

    public static string? RunInProcessParser(string toolsetDirectory)
    {
        if (!TryGetPythonHome(out _, out _))
        {
            Assert.Inconclusive(OptionalArtifact.PixiPythonHint);
            return null;
        }

        OptionalArtifact.RequireFile(GetToolParserScriptPath(), $"ToolParser.py not found at '{GetToolParserScriptPath()}'.");

        BindPixiPython();

        using (Py.GIL())
        {
            using var scope = Py.CreateScope();
            scope.Set("__toolset_directory__", new PyString(toolsetDirectory));
            scope.Exec(LoadToolParserScript());
            var pyResult = scope.Get("__parser_result__");
            return pyResult?.As<string>();
        }
    }

    public static void BindPixiPython()
    {
        if (!TryGetPythonHome(out var pythonHome, out var pythonDll))
        {
            Assert.Inconclusive(OptionalArtifact.PixiPythonHint);
            return;
        }

        EnsurePythonInitialized(pythonHome, pythonDll);
    }

    public static string GetToolParserScriptPath() =>
        Path.Combine(FindRepositoryRoot(), "source", "DevTools.Execution", "Resources", "scripts", "ToolParser.py");

    private static void EnsurePythonInitialized(string pythonHome, string pythonDll)
    {
        lock (InitLock)
        {
            if (_initialized)
                return;

            PythonNativeEnvironment.PrepareProcess(pythonHome);

            Runtime.PythonDLL = pythonDll;
            PythonEngine.PythonHome = pythonHome;
            PythonEngine.ProgramName = "RevitDevToolTests";
            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();
            using (Py.GIL())
                PythonNativeEnvironment.AddPythonDllDirectories(pythonHome);

            _initialized = true;
        }
    }

    private static bool TryGetPythonHome(out string pythonHome, out string pythonDll)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        pythonHome = Path.Combine(appData, "RevitDevTool", "pixi-env", ".pixi", "envs", "default");
        pythonDll = FindPythonDll(pythonHome);
        return Directory.Exists(pythonHome)
            && File.Exists(pythonDll)
            && File.Exists(OptionalArtifact.PixiPythonExePath);
    }

    private static string FindPythonDll(string pythonHome)
    {
        var dll = Directory.Exists(pythonHome)
            ? Directory.GetFiles(pythonHome, "python3*.dll")
                .FirstOrDefault(path => !PythonNativeEnvironment.IsStableAbiForwarder(path))
            : null;

        return dll ?? Path.Combine(pythonHome, "python3.dll");
    }

    private static string LoadToolParserScript() => File.ReadAllText(GetToolParserScriptPath());

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "RevitDevTool.slnx"))
                || File.Exists(Path.Combine(current.FullName, "RevitDevTool.sln")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the RevitDevTool repository root.");
    }
}
