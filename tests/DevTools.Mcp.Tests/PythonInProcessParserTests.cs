using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Python.Runtime;
namespace DevTools.Mcp.Tests;

public sealed class PythonInProcessParserTests : IDisposable
{
    private static readonly PythonToolsetParser Parser = new(NullLogger<PythonToolsetParser>.Instance);
    private static readonly string PythonHome;
    private static readonly string PythonDll;
    private static readonly string ToolParserScript;
    private static readonly string ToolsetDirectory;
    private static readonly Lock InitLock = new();
    private static bool _initialized;

    static PythonInProcessParserTests()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        PythonHome = Path.Combine(appData, "RevitDevTool", "pixi-env", ".pixi", "envs", "default");
        PythonDll = FindPythonDll(PythonHome);
        ToolParserScript = LoadToolParserScript();
        ToolsetDirectory = Path.Combine(FindRepositoryRoot(), "samples", "PythonDemo", "mcp_toolset");
    }

    public PythonInProcessParserTests()
    {
        EnsurePythonInitialized();
    }

    public void Dispose()
    {
    }

    [Fact]
    public void InProcess_ParsesAnnotationSample_Tools()
    {
        var result = RunInProcessParser(ToolsetDirectory);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result));

        var catalog = Parser.ParseDirectoryCatalog(ToolsetDirectory, _ => result);
        var tool = catalog.Tools.SingleOrDefault(t => t.Descriptor.Name == "get_parser_sample_status");

        Assert.NotNull(tool);
        Assert.Equal("Get Parser Sample Status", tool.Descriptor.Annotations!.Title);
        Assert.True(tool.Descriptor.Annotations.ReadOnly);
        Assert.True(tool.Descriptor.Annotations.Idempotent);
        Assert.False(tool.Descriptor.Annotations.OpenWorld);
    }

    [Fact]
    public void InProcess_ParsesAnnotationSample_Resources()
    {
        var result = RunInProcessParser(ToolsetDirectory);

        Assert.NotNull(result);

        var catalog = Parser.ParseDirectoryCatalog(ToolsetDirectory, _ => result);
        var direct = catalog.Resources.SingleOrDefault(r => r.Descriptor?.Name == "parser_status_resource");
        var template = catalog.Resources.SingleOrDefault(r => r.TemplateDescriptor?.Name == "parser_view_resource");

        Assert.NotNull(direct);
        Assert.NotNull(template);
        Assert.Equal("sample://parser/status", direct.Descriptor!.Uri);
        Assert.NotNull(template.TemplateDescriptor);
    }

    [Fact]
    public void InProcess_ParsesLowLevelSample()
    {
        var result = RunInProcessParser(ToolsetDirectory);

        Assert.NotNull(result);

        var catalog = Parser.ParseDirectoryCatalog(ToolsetDirectory, _ => result);
        var tool = catalog.Tools.SingleOrDefault(t => t.Descriptor.Name == "parser_lowlevel_tool");
        var resource = catalog.Resources.SingleOrDefault(r => r.Descriptor?.Name == "parser_lowlevel_resource");

        Assert.NotNull(tool);
        Assert.NotNull(resource);
        Assert.Equal("Parser Low-Level Tool", tool.Descriptor.Title);
    }

    [Fact]
    public void InProcess_OutputIsValidJson()
    {
        var result = RunInProcessParser(ToolsetDirectory);

        Assert.NotNull(result);

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("tools", out _));
        Assert.True(doc.RootElement.TryGetProperty("resources", out _));
        Assert.False(doc.RootElement.TryGetProperty("prompts", out _));
    }

    [Fact]
    public void InProcess_MatchesOutOfProcessOutput()
    {
        var pythonExe = Path.Combine(PythonHome, "python.exe");
        var toolParserScriptPath = Path.Combine(FindRepositoryRoot(), "source", "DevTools.Execution", "Resources", "scripts", "ToolParser.py");

        Assert.True(File.Exists(pythonExe), $"Python executable not found at '{pythonExe}'.");
        Assert.True(File.Exists(toolParserScriptPath), $"ToolParser.py not found at '{toolParserScriptPath}'.");

        var outOfProcess = Parser.ParseDirectoryCatalog(ToolsetDirectory, pythonExe, toolParserScriptPath);
        var inProcessJson = RunInProcessParser(ToolsetDirectory);

        Assert.NotNull(inProcessJson);

        var inProcess = Parser.ParseDirectoryCatalog(ToolsetDirectory, _ => inProcessJson);

        Assert.Equal(outOfProcess.Tools.Count, inProcess.Tools.Count);
        Assert.Equal(outOfProcess.Resources.Count, inProcess.Resources.Count);

        foreach (var oopTool in outOfProcess.Tools)
        {
            var ipTool = inProcess.Tools.SingleOrDefault(t => t.Descriptor.Name == oopTool.Descriptor.Name);
            Assert.NotNull(ipTool);
            Assert.Equal(oopTool.Descriptor.Title, ipTool.Descriptor.Title);
            Assert.Equal(oopTool.Descriptor.Description, ipTool.Descriptor.Description);
        }
    }

    private static string? RunInProcessParser(string toolsetDirectory)
    {
        using (Py.GIL())
        {
            using var scope = Py.CreateScope();
            scope.Set("__toolset_directory__", new PyString(toolsetDirectory));
            scope.Exec(ToolParserScript);
            var pyResult = scope.Get("__parser_result__");
            return pyResult?.As<string>();
        }
    }

    private static void EnsurePythonInitialized()
    {
        lock (InitLock)
        {
            if (_initialized) return;

            Assert.True(Directory.Exists(PythonHome), $"Pixi Python env not found at '{PythonHome}'.");
            Assert.True(File.Exists(PythonDll), $"Python DLL not found at '{PythonDll}'.");

            var libraryBin = Path.Combine(PythonHome, "Library", "bin");
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var toAdd = new[] { PythonHome, libraryBin }
                .Where(Directory.Exists)
                .Where(d => currentPath.IndexOf(d, StringComparison.OrdinalIgnoreCase) < 0)
                .ToList();
            if (toAdd.Count > 0)
                Environment.SetEnvironmentVariable("PATH", string.Join(";", toAdd) + ";" + currentPath);

            Runtime.PythonDLL = PythonDll;
            PythonEngine.PythonHome = PythonHome;
            PythonEngine.ProgramName = "RevitDevToolTests";
            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();

            _initialized = true;
        }
    }

    private static string FindPythonDll(string pythonHome)
    {
        var exact = Path.Combine(pythonHome, "python313.dll");
        if (File.Exists(exact)) return exact;

        if (Directory.Exists(pythonHome))
        {
            var dll = Directory.GetFiles(pythonHome, "python*.dll").FirstOrDefault();
            if (dll is not null) return dll;
        }

        return exact;
    }

    private static string LoadToolParserScript()
    {
        var path = Path.Combine(FindRepositoryRoot(), "source", "DevTools.Execution", "Resources", "scripts", "ToolParser.py");
        Assert.True(File.Exists(path), $"ToolParser.py not found at '{path}'.");
        return File.ReadAllText(path);
    }

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
