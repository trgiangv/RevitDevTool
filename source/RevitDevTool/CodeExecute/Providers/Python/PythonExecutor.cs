using Python.Included;
using Python.Runtime;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RevitDevTool.CodeExecute.Providers.Python;

/// <summary>
/// Manages Python runtime initialization and script execution using Python.NET.
/// Replaces Dynamo-based Python execution with direct PythonNet integration.
/// </summary>
public static class PythonExecutor
{
    private static bool _isInitialized;
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    /// <summary>
    /// Initialize Python runtime. Safe to call multiple times (idempotent).
    /// </summary>
    public static async Task InitializeAsync()
    {
        if (_isInitialized) return;

        await InitLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isInitialized) return;

            if (!Installer.IsPythonInstalled())
            {
                await Installer.SetupPython().ConfigureAwait(false);
            }

            if (!Installer.IsPipInstalled())
            {
                await Installer.TryInstallPip().ConfigureAwait(false);
            }

            if (!PythonEngine.IsInitialized)
            {
                Runtime.PythonDLL = Path.Combine(Installer.EmbeddedPythonHome, "python313.dll");
                PythonEngine.PythonHome = Installer.EmbeddedPythonHome;
                PythonEngine.ProgramName = "RevitDevTool";
                PythonEngine.Initialize();
                PythonEngine.BeginAllowThreads();
            }

            _isInitialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }

    /// <summary>
    /// Install a Python module using pip.
    /// </summary>
    public static async Task InstallModuleAsync(string moduleName, string version = "")
    {
        if (!Installer.IsModuleInstalled(moduleName))
        {
            Trace.TraceInformation($"Installing Python module: {moduleName}");
            await Installer.PipInstallModule(moduleName, version).ConfigureAwait(false);
            Trace.TraceInformation($"Module installed: {moduleName}");
        }
    }

    /// <summary>
    /// Execute a Python script file with Revit context.
    /// </summary>
    public static void ExecuteScript(string scriptPath, string? rootFolder = null)
    {
        ValidateRuntime(scriptPath);

        var code = File.ReadAllText(scriptPath);
        rootFolder ??= Path.GetDirectoryName(scriptPath) ?? string.Empty;

        using (Py.GIL())
        {
            using (var scope = Py.CreateScope("__main__"))
            {
                SetupScopeVariables(scope, scriptPath, rootFolder);
                SetupOutputRedirection(scope);

                try
                {
                    scope.Exec(code);
                }
                catch (PythonException ex)
                {
                    var traceMessage = StackTraceBuilder.Build(ex, scriptPath, code);
                    Trace.TraceError(traceMessage);
                }
            }
        }
    }

    private static void ValidateRuntime(string scriptPath)
    {
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"Python script not found: {scriptPath}");

        if (!_isInitialized)
            throw new InvalidOperationException("Python runtime not initialized. Call InitializeAsync() first.");
    }

    private static void SetupScopeVariables(PyModule scope, string scriptPath, string rootFolder)
    {
        Action<object> logFunction = obj => Trace.Write(obj);
        
        scope.Set("__file__", scriptPath);
        scope.Set("__root__", rootFolder);
        scope.Set("__revit__", Context.UiApplication);
        scope.Set("__log_func__", logFunction.ToPython());
    }

    private static void SetupOutputRedirection(PyModule scope)
    {
        // Improved redirection:
        // 1. Appends rootFolder to sys.path
        // 2. Overrides print to join args correctly and send to log_func in one go
        // 3. Redirects stdout/stderr to log_func directly
        const string setupCode = """
                                 import sys
                                 import builtins
                                 
                                 if __root__ not in sys.path:
                                     sys.path.append(__root__)
                                 
                                 def custom_print(*args, sep=' ', end='\n'):
                                     # To use Trace Visualization, pass objects as separate arguments: print("Label", obj)
                                 
                                     # Case 1: Single Argument -> Pass Raw Object (Enable Trace)
                                     if len(args) == 1:
                                         __log_func__(args[0])
                                         if end != '\n': 
                                             __log_func__(end)
                                         return
                                 
                                     # Case 2: Mixed Content containing Complex Objects
                                     # If we just str(obj), we lose Trace ability. 
                                     # If using default separator, we split them into separate logs to preserve objects.
                                     has_complex = any(not isinstance(a, (str, int, float, bool, type(None))) for a in args)
                                     
                                     if has_complex and sep == ' ':
                                         for arg in args:
                                             __log_func__(arg)
                                         if end != '\n': 
                                             __log_func__(end)
                                         return
                                 
                                     # Case 3: Simple Text or Custom Separator -> Standard Join
                                     text = sep.join(str(arg) for arg in args) + end
                                     __log_func__(text)
                                 
                                 # Override built-in print
                                 builtins.print = custom_print
                                 
                                 # Redirect stdout/stderr
                                 class StdOutRedirector:
                                     def __init__(self, log_func):
                                         self.log_func = log_func
                                     def write(self, text):
                                         # Avoid empty newlines from being logged separately if possible
                                         if text != '\n':
                                             self.log_func(text)
                                     def flush(self):
                                         pass
                                 
                                 sys.stdout = StdOutRedirector(__log_func__)
                                 sys.stderr = StdOutRedirector(__log_func__)
                                 """;
        scope.Exec(setupCode);
    }
}

/// <summary>
/// Helper class to build clean python stack traces
/// </summary>
file static class StackTraceBuilder
{
    private static readonly Regex LineNumberRegex = new(@",\s*line\s*(?<lineno>\d+),", RegexOptions.Compiled);
    private static readonly Regex TomlValueRegex = new("'(?<value>.+)'", RegexOptions.Compiled);

    public static string Build(PythonException ex, string sourceFile, string sourceContent)
    {
        var (pyTraceback, dotNetTraceback) = SplitTraceback(ex.StackTrace);
        var cleanedPyTraceback = CleanPythonTraceback(pyTraceback, sourceFile, sourceContent);
        
        return string.Join("\n", ex.Message, cleanedPyTraceback, ex.Source, dotNetTraceback).NormalizeNewLine();
    }

    private static (string PyTraceback, string DotNetTraceback) SplitTraceback(string? stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace)) 
            return (string.Empty, string.Empty);

        var parts = stackTrace!.Split(']');
        if (parts.Length < 2) 
            return (stackTrace, string.Empty);

        // Python trace is usually the first part enclosed in brackets if it's a list string
        var pyTracepart = parts[0].Trim() + "]"; 
        var dotNetPart = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        
        return (pyTracepart, dotNetPart);
    }

    private static string CleanPythonTraceback(string rawTraceback, string sourceFile, string sourceContent)
    {
        var sb = new StringBuilder();
        var lines = ParseTomlListString(rawTraceback);

        foreach (var line in lines)
        {
            if (line.Contains("File \"<string>\""))
            {
                ProcessSourceLine(sb, line, sourceFile, sourceContent);
            }
            else
            {
                sb.AppendLine(line);
            }
        }

        return sb.ToString();
    }

    private static void ProcessSourceLine(StringBuilder sb, string line, string sourceFile, string sourceContent)
    {
        var fixedLine = line.Replace("File \"<string>\"", $"File \"{sourceFile}\"");
        sb.Append(fixedLine);

        var match = LineNumberRegex.Match(line);
        if (!match.Success) return;

        if (!int.TryParse(match.Groups["lineno"].Value, out var lineNo)) return;
        var sourceLines = sourceContent.Split('\n');
        var index = lineNo - 1;
        if (index < 0 || index >= sourceLines.Length) return;
        sb.AppendLine(); // Add newline after the File line
        sb.AppendLine(sourceLines[index].Trim()); // Add the actual code line
    }

    private static IEnumerable<string> ParseTomlListString(string tomlListString)
    {
        // Simple manual parsing to avoid heavy dependencies for just this string format
        // Expected format: ['line 1', 'line 2', ...]
        var content = tomlListString.Trim('[', ']');
        if (string.IsNullOrWhiteSpace(content))
            yield break;

        // Split by comma but respect quotes is hard with simple split. 
        // Assuming standard python list repr format, we can try matching quotes.
        var matches = TomlValueRegex.Matches(content);
        foreach (Match match in matches)
        {
            yield return match.Groups["value"].Value;
        }
    }

    private static string NormalizeNewLine(this string input)
    {
        return input.Replace("\r\n", "\n");
    }
}