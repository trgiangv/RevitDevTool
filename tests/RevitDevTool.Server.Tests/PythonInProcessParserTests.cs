using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Python.Runtime;
namespace RevitDevTool.Server.Tests;

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
        var tool = catalog.Tools.SingleOrDefault(t => t.ProtocolTool.Name == "get_parser_sample_status");

        Assert.NotNull(tool);
        Assert.Equal("Get Parser Sample Status", tool.ProtocolTool.Annotations!.Title);
        Assert.True(tool.ProtocolTool.Annotations.ReadOnlyHint);
        Assert.True(tool.ProtocolTool.Annotations.IdempotentHint);
        Assert.False(tool.ProtocolTool.Annotations.OpenWorldHint);
    }

    [Fact]
    public void InProcess_ParsesAnnotationSample_Prompts()
    {
        var result = RunInProcessParser(ToolsetDirectory);

        Assert.NotNull(result);

        var catalog = Parser.ParseDirectoryCatalog(ToolsetDirectory, _ => result);
        var prompt = catalog.Prompts.SingleOrDefault(p => p.ProtocolPrompt.Name == "summarize_parser_sample");

        Assert.NotNull(prompt);
        Assert.Equal("Summarize Parser Sample", prompt.ProtocolPrompt.Title);
        Assert.Equal(2, prompt.ProtocolPrompt.Arguments!.Count);
    }

    [Fact]
    public void InProcess_ParsesAnnotationSample_Resources()
    {
        var result = RunInProcessParser(ToolsetDirectory);

        Assert.NotNull(result);

        var catalog = Parser.ParseDirectoryCatalog(ToolsetDirectory, _ => result);
        var direct = catalog.Resources.SingleOrDefault(r => r.ProtocolResource?.Name == "parser_status_resource");
        var template = catalog.Resources.SingleOrDefault(r => r.ProtocolTemplate?.Name == "parser_view_resource");

        Assert.NotNull(direct);
        Assert.NotNull(template);
        Assert.Equal("sample://parser/status", direct.ProtocolResource!.Uri);
        Assert.True(template.ProtocolTemplate!.IsTemplated);
    }

    [Fact]
    public void InProcess_ParsesLowLevelSample()
    {
        var result = RunInProcessParser(ToolsetDirectory);

        Assert.NotNull(result);

        var catalog = Parser.ParseDirectoryCatalog(ToolsetDirectory, _ => result);
        var tool = catalog.Tools.SingleOrDefault(t => t.ProtocolTool.Name == "parser_lowlevel_tool");
        var prompt = catalog.Prompts.SingleOrDefault(p => p.ProtocolPrompt.Name == "parser_lowlevel_prompt");
        var resource = catalog.Resources.SingleOrDefault(r => r.ProtocolResource?.Name == "parser_lowlevel_resource");

        Assert.NotNull(tool);
        Assert.NotNull(prompt);
        Assert.NotNull(resource);
        Assert.Equal("Parser Low-Level Tool", tool.ProtocolTool.Title);
    }

    [Fact]
    public void InProcess_OutputIsValidJson()
    {
        var result = RunInProcessParser(ToolsetDirectory);

        Assert.NotNull(result);

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("tools", out _));
        Assert.True(doc.RootElement.TryGetProperty("prompts", out _));
        Assert.True(doc.RootElement.TryGetProperty("resources", out _));
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
        Assert.Equal(outOfProcess.Prompts.Count, inProcess.Prompts.Count);
        Assert.Equal(outOfProcess.Resources.Count, inProcess.Resources.Count);

        foreach (var oopTool in outOfProcess.Tools)
        {
            var ipTool = inProcess.Tools.SingleOrDefault(t => t.ProtocolTool.Name == oopTool.ProtocolTool.Name);
            Assert.NotNull(ipTool);
            Assert.Equal(oopTool.ProtocolTool.Title, ipTool.ProtocolTool.Title);
            Assert.Equal(oopTool.ProtocolTool.Description, ipTool.ProtocolTool.Description);
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
