using Python.Included;
using Python.Runtime;
using System.Diagnostics;
using System.IO;
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

                Trace.TraceInformation("Python runtime initialized successfully.");
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
    /// <param name="moduleName">Name of the module to install (e.g., "spacy")</param>
    public static async Task InstallModuleAsync(string moduleName)
    {
        if (!Installer.IsModuleInstalled(moduleName))
        {
            Trace.TraceInformation($"Installing Python module: {moduleName}");
            await Installer.PipInstallModule(moduleName).ConfigureAwait(false);
            Trace.TraceInformation($"Module installed: {moduleName}");
        }
    }

    /// <summary>
    /// Execute a Python script file with Revit context.
    /// </summary>
    /// <param name="scriptPath">Full path to the .py script file</param>
    /// <param name="rootFolder">Root folder to add to sys.path (optional)</param>
    public static void ExecuteScript(string scriptPath, string? rootFolder = null)
    {
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Python script not found: {scriptPath}");
        }

        if (!_isInitialized)
        {
            throw new InvalidOperationException("Python runtime not initialized. Call InitializeAsync() first.");
        }

        var code = File.ReadAllText(scriptPath);
        rootFolder ??= Path.GetDirectoryName(scriptPath) ?? string.Empty;

        using (Py.GIL())
        {
            using (var scope = Py.CreateScope("__main__"))
            {
                // Setup logging callback
                Action<object> logFunction = obj => Trace.Write(obj);

                // Setup scope variables
                scope.Set("__file__", scriptPath);
                scope.Set("__root__", rootFolder);
                scope.Set("__revit__", Context.UiApplication);
                scope.Set("__log_func__", logFunction.ToPython());

                // Execute setup code to override print and redirect stdout
                const string setupCode = """
                                         import sys
                                         import builtins
                                         if __root__ not in sys.path:
                                             sys.path.append(__root__)

                                         def custom_print(*args, sep=' ', end='\n'):
                                             for arg in args:
                                                 __log_func__(arg)
                                             if end:
                                                 __log_func__(end)

                                         # Override built-in print
                                         builtins.print = custom_print

                                         # Redirect stdout
                                         class StdOutRedirector:
                                             def __init__(self, log_func):
                                                 self.text = ''
                                                 self.log_func = log_func
                                             def write(self, text):
                                                 if text == '\n':
                                                     self.log_func(self.text)
                                                     self.text = ''
                                                 else:
                                                     self.text += text

                                         sys.stdout = StdOutRedirector(__log_func__)
                                         """;

                scope.Exec(setupCode);

                try
                {
                    scope.Exec(code);
                    Trace.TraceInformation($"Python script executed successfully: {scriptPath}");
                }
                catch (PythonException ex)
                {
                    var traceMessage = BuildPythonStackTrace(ex, scriptPath, code);
                    Trace.TraceError(traceMessage);
                    throw;
                }
            }
        }
    }

    #region Private Helpers

    private static string BuildPythonStackTrace(PythonException cpythonException, string sourceFile, string sourceContent)
    {
        var cleanedPyTraceback = string.Empty;
        var pyNetTraceback = string.Empty;

        if (!string.IsNullOrWhiteSpace(cpythonException.StackTrace))
        {
            var traceBackParts = cpythonException.StackTrace.Split(']');
            var nextIdx = 0;

            // If stack trace contains file info, clean it up
            if (traceBackParts.Length == 2)
            {
                nextIdx = 1;
                var pyTraceback = traceBackParts[0].Trim() + "]";
                cleanedPyTraceback = string.Empty;

                foreach (var tbLine in pyTraceback.ConvertFromTomlListString())
                {
                    if (tbLine.Contains("File \"<string>\""))
                    {
                        var fixedTbLine = tbLine.Replace("File \"<string>\"", $"File \"{sourceFile}\"");
                        cleanedPyTraceback += fixedTbLine;

                        var lineNo = new Regex(@",\s*line\s*(?<lineno>\d+),").Match(tbLine).Groups["lineno"].Value;
                        if (!string.IsNullOrEmpty(lineNo))
                        {
                            var lines = sourceContent.Split('\n');
                            var lineIndex = int.Parse(lineNo.Trim()) - 1;
                            if (lineIndex >= 0 && lineIndex < lines.Length)
                            {
                                cleanedPyTraceback += lines[lineIndex] + "\n";
                            }
                        }
                    }
                    else
                    {
                        cleanedPyTraceback += tbLine;
                    }
                }
            }

            // Grab the dotnet cpython stack trace
            if (nextIdx < traceBackParts.Length)
            {
                pyNetTraceback = traceBackParts[nextIdx].Trim();
            }
        }

        var traceMessage = string.Join("\n", cpythonException.Message, cleanedPyTraceback, cpythonException.Source, pyNetTraceback);

        return traceMessage.NormalizeNewLine();
    }

    private static List<string> ConvertFromTomlListString(this string tomlListString)
    {
        var text = tomlListString.Replace("[", "").Replace("]", "");
        var list = new List<string>(text.Split(','));
        var list2 = new List<string>();
        var regex = new Regex("'(?<value>.+)'");

        foreach (var item in list)
        {
            var match = regex.Match(item);
            if (match.Success)
            {
                list2.Add(match.Groups["value"].Value);
            }
        }

        return list2;
    }

    private static string NormalizeNewLine(this string input)
    {
        return input.Replace("\r\n", "\n");
    }

    #endregion
}