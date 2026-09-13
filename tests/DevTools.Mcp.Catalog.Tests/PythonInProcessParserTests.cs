using System.Text.Json;
using DevTools.Execution.Providers.Python;
using Microsoft.Extensions.Logging.Abstractions;
using Python.Runtime;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class PythonInProcessParserTests : IDisposable
{
    private static readonly PythonToolsetParser Parser = new(NullLogger<PythonToolsetParser>.Instance);
    private static readonly Lock InitLock = new();
    private static bool _initialized;

    public void Dispose()
    {
    }

    [TestMethod]
    public void InProcess_ParsesAnnotationSample_Tools()
    {
        RequirePythonRuntime();
        var toolsetDirectory = GetToolsetDirectory();
        var result = RunInProcessParser(toolsetDirectory);

        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result));

        var catalog = Parser.ParseDirectoryCatalog(toolsetDirectory, _ => result);
        var tool = catalog.Tools.SingleOrDefault(t => t.Descriptor.Name == "get_parser_sample_status");

        Assert.IsNotNull(tool);
        Assert.AreEqual("Get Parser Sample Status", tool.Descriptor.Annotations!.Title);
        Assert.IsTrue(tool.Descriptor.Annotations.ReadOnlyHint);
        Assert.IsTrue(tool.Descriptor.Annotations.IdempotentHint);
        Assert.IsFalse(tool.Descriptor.Annotations.OpenWorldHint);
    }

    [TestMethod]
    public void InProcess_ParsesAnnotationSample_Resources()
    {
        RequirePythonRuntime();
        var toolsetDirectory = GetToolsetDirectory();
        var result = RunInProcessParser(toolsetDirectory);

        Assert.IsNotNull(result);

        var catalog = Parser.ParseDirectoryCatalog(toolsetDirectory, _ => result);
        var direct = catalog.Resources.SingleOrDefault(r => r.Descriptor?.Name == "parser_status_resource");
        var template = catalog.Resources.SingleOrDefault(r => r.TemplateDescriptor?.Name == "parser_view_resource");

        Assert.IsNotNull(direct);
        Assert.IsNotNull(template);
        Assert.AreEqual("sample://parser/status", direct.Descriptor!.Uri);
        Assert.IsNotNull(template.TemplateDescriptor);
    }

    [TestMethod]
    public void InProcess_ParsesLowLevelSample()
    {
        RequirePythonRuntime();
        var toolsetDirectory = GetToolsetDirectory();
        var result = RunInProcessParser(toolsetDirectory);

        Assert.IsNotNull(result);

        var catalog = Parser.ParseDirectoryCatalog(toolsetDirectory, _ => result);
        var tool = catalog.Tools.SingleOrDefault(t => t.Descriptor.Name == "parser_lowlevel_tool");
        var resource = catalog.Resources.SingleOrDefault(r => r.Descriptor?.Name == "parser_lowlevel_resource");

        Assert.IsNotNull(tool);
        Assert.IsNotNull(resource);
        Assert.AreEqual("Parser Low-Level Tool", tool.Descriptor.Title);
    }

    [TestMethod]
    public void InProcess_OutputIsValidJson()
    {
        RequirePythonRuntime();
        var toolsetDirectory = GetToolsetDirectory();
        var result = RunInProcessParser(toolsetDirectory);

        Assert.IsNotNull(result);

        var doc = JsonDocument.Parse(result);
        Assert.IsTrue(doc.RootElement.TryGetProperty("tools", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("resources", out _));
        Assert.IsFalse(doc.RootElement.TryGetProperty("prompts", out _));
    }

    [TestMethod]
    public void InProcess_MatchesOutOfProcessOutput()
    {
        RequirePythonRuntime();
        var toolsetDirectory = GetToolsetDirectory();
        var pythonExe = OptionalArtifact.PixiPythonExePath;
        var toolParserScriptPath = GetToolParserScriptPath();

        OptionalArtifact.RequireFile(pythonExe, OptionalArtifact.PixiPythonHint);
        OptionalArtifact.RequireFile(toolParserScriptPath, $"ToolParser.py not found at '{toolParserScriptPath}'.");

        var outOfProcess = Parser.ParseDirectoryCatalog(toolsetDirectory, pythonExe, toolParserScriptPath);
        var inProcessJson = RunInProcessParser(toolsetDirectory);

        Assert.IsNotNull(inProcessJson);

        var inProcess = Parser.ParseDirectoryCatalog(toolsetDirectory, _ => inProcessJson);

        Assert.AreEqual(outOfProcess.Tools.Count, inProcess.Tools.Count);
        Assert.AreEqual(outOfProcess.Resources.Count, inProcess.Resources.Count);

        foreach (var oopTool in outOfProcess.Tools)
        {
            var ipTool = inProcess.Tools.SingleOrDefault(t => t.Descriptor.Name == oopTool.Descriptor.Name);
            Assert.IsNotNull(ipTool);
            Assert.AreEqual(oopTool.Descriptor.Title, ipTool.Descriptor.Title);
            Assert.AreEqual(oopTool.Descriptor.Description, ipTool.Descriptor.Description);
        }
    }

    private static string? RunInProcessParser(string toolsetDirectory)
    {
        using (Py.GIL())
        {
            using var scope = Py.CreateScope();
            scope.Set("__toolset_directory__", new PyString(toolsetDirectory));
            scope.Exec(LoadToolParserScript());
            var pyResult = scope.Get("__parser_result__");
            return pyResult?.As<string>();
        }
    }

    private static void RequirePythonRuntime()
    {
        if (!TryGetPythonHome(out var pythonHome, out var pythonDll))
            Assert.Inconclusive(OptionalArtifact.PixiPythonHint);

        var scriptPath = GetToolParserScriptPath();
        OptionalArtifact.RequireFile(scriptPath, $"ToolParser.py not found at '{scriptPath}'.");

        OptionalArtifact.RequireDirectory(GetToolsetDirectory(), $"Expected Python sample toolset at '{GetToolsetDirectory()}'.");

        try
        {
            EnsurePythonInitialized(pythonHome, pythonDll);
        }
        catch (Exception ex) when (ex is TypeInitializationException or MissingMethodException or DllNotFoundException or BadImageFormatException)
        {
            Assert.Inconclusive($"pythonnet cannot bind this pixi Python: {ex.GetBaseException().Message}");
        }
    }

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
        var exact = Path.Combine(pythonHome, "python313.dll");
        if (File.Exists(exact))
            return exact;

        if (Directory.Exists(pythonHome))
        {
            var dll = Directory.GetFiles(pythonHome, "python*.dll").FirstOrDefault();
            if (dll is not null)
                return dll;
        }

        return exact;
    }

    private static string LoadToolParserScript()
    {
        var path = GetToolParserScriptPath();
        OptionalArtifact.RequireFile(path, $"ToolParser.py not found at '{path}'.");
        return File.ReadAllText(path);
    }

    private static string GetToolParserScriptPath() =>
        Path.Combine(FindRepositoryRoot(), "source", "DevTools.Execution", "Resources", "scripts", "ToolParser.py");

    private static string GetToolsetDirectory() =>
        Path.Combine(FindRepositoryRoot(), "samples", "PythonDemo", "mcp_toolset");

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
