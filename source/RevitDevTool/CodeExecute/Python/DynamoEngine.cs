using System.Diagnostics;
using System.IO;
using System.Reflection;
using RevitDevTool.Controllers;

namespace RevitDevTool.CodeExecute.Python;

public static class DynamoEngine
{
    private const string ShowUiKey = "dynShowUI";
    private const string AutomationModeKey = "dynAutomation";
    private const string DynPathKey = "dynPath";
    private const string ForceManualRunKey = "dynForceManualRun";
    private const string ModelShutDownKey = "dynModelShutDown";

    private const string DynamoPlayer = "DYNAMOPLAYER";
    private const string DynamoRevitDs = "DynamoRevitDS";
    private const string DynamoRevit = "Dynamo.Applications.DynamoRevit";
    private const int DynamoModelStateRunning = 2; // DynamoModel.DynamoModelState.StartedUI
    private const string ModelState = "ModelState";
    private const string DynamoRevitCommandData = "Dynamo.Applications.DynamoRevitCommandData";

    private const string JournalData = "JournalData";
    private const string Application = "Application";
    private const string ExecuteCommand = "ExecuteCommand";
    private const string RevitDynamoModel = "RevitDynamoModel";
    private const string ForceRun = "ForceRun";
    private const string Logger = "Logger";
    private const string CliMode = "cliMode";

    private static Assembly? _cachedAssembly;
    private static object? _cachedDynamoRevitInstance;
    private static PropertyInfo? _modelStateProperty;

    private static bool IsDynamoUiRunning()
    {
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assembly = _cachedAssembly ??= assemblies.FirstOrDefault(a => a.FullName != null && a.FullName.Contains(DynamoRevitDs));
            var dynamoRevit = _cachedDynamoRevitInstance ??= assembly?.CreateInstance(DynamoRevit);
            var dynamoModelStateProp = _modelStateProperty ??= dynamoRevit?.GetType().GetProperty(ModelState);
            var dynamoModelState = dynamoModelStateProp?.GetValue(dynamoRevit);
            if (dynamoModelState is not null && (int)dynamoModelState == DynamoModelStateRunning)
            {
                return true;
            }

            if (Process.GetProcesses().Any(process =>
                    process.ProcessName.ToUpper().Replace(" ", "").Equals(DynamoPlayer) ||
                    process.MainWindowTitle.ToUpper().Replace(" ", "").Equals(DynamoPlayer)))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[RevitDevTool] DynamoEngine.IsDynamoUiRunning: {ex.Message}");
        }
        return false;
    }

    private static void DisableDynamoLogger(object? revitDynamoModel)
    {
        if (revitDynamoModel == null) return;

        try
        {
            var loggerField = revitDynamoModel.GetType().GetField(Logger,
                BindingFlags.Public | BindingFlags.Instance);

            if (loggerField == null)
            {
                loggerField = revitDynamoModel.GetType().BaseType?
                    .GetField(Logger, BindingFlags.Public | BindingFlags.Instance);
            }

            if (loggerField == null) return;
            var logger = loggerField.GetValue(revitDynamoModel);

            if (logger == null) return;
            var cliModeField = logger.GetType().GetField(CliMode,
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (cliModeField != null)
            {
                cliModeField.SetValue(logger, true);
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void SetToAutomatic(string filePath)
    {
        var text = File.ReadAllText(filePath);
        text = text.Replace(@"""RunType"": ""Manual"",", @"""RunType"": ""Automatic"",");
        File.WriteAllText(filePath, text);
    }

    /// <summary>
    /// Run Dynamo graph using ExternalEvent to ensure valid Revit API context.
    /// </summary>
    internal static void RunDynamoGraph(string dynamoPath)
    {
        if (string.IsNullOrEmpty(dynamoPath) || !File.Exists(dynamoPath) ||
            !dynamoPath.EndsWith(".dyn", StringComparison.OrdinalIgnoreCase))
        {
            Trace.TraceError($"[RevitDevTool] DynamoEngine: Invalid path: {dynamoPath}");
            return;
        }

        ExternalEventController.ActionEventHandler.Raise(_ => RunDynamoGraphCore(dynamoPath));
    }

    /// <summary>
    /// https://github.com/johnpierson/Relay
    /// </summary>
    private static void RunDynamoGraphCore(string dynamoPath)
    {
        try
        {
            var isDynamoUiRunning = IsDynamoUiRunning();
            if (isDynamoUiRunning)
            {
                Trace.TraceWarning("Please close Dynamo or Dynamo Player before run");
                return;
            }

            SetToAutomatic(dynamoPath);

            IDictionary<string, string> journalData = new Dictionary<string, string>
            {
                { ShowUiKey, false.ToString() },
                { AutomationModeKey, true.ToString() },
                { DynPathKey, dynamoPath },
                { ForceManualRunKey, true.ToString() },
                { ModelShutDownKey, true.ToString() },
            };

            // DynamoRevitDs assembly
            _cachedAssembly ??= AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.FullName != null && a.FullName.Contains(DynamoRevitDs));

            // DynamoRevit instance
            _cachedDynamoRevitInstance ??= _cachedAssembly?.CreateInstance(DynamoRevit);

            // Create command data (new each time - journal data changes)
            var dta = _cachedAssembly?.CreateInstance(DynamoRevitCommandData);
            dta?.GetType().GetProperty(Application)?.SetValue(dta, Context.UiApplication, null);
            dta?.GetType().GetProperty(JournalData)?.SetValue(dta, journalData, null);

            // Initialized Dynamo
            var originalOut = Console.Out;
            var originalError = Console.Error;
            
            using (var nullWriter = TextWriter.Null)
            {
                try
                {
                    Console.SetOut(nullWriter);
                    Console.SetError(nullWriter);
                    _cachedDynamoRevitInstance?.GetType().GetMethod(ExecuteCommand)?.Invoke(_cachedDynamoRevitInstance, [dta]);
                }
                finally
                {
                    Console.SetOut(originalOut);
                    Console.SetError(originalError);
                }
            }

            // Force run
            var rdm = _cachedDynamoRevitInstance?.GetType().GetProperty(RevitDynamoModel)?.GetValue(_cachedDynamoRevitInstance);
            DisableDynamoLogger(rdm);
            rdm?.GetType().GetMethod(ForceRun)?.Invoke(rdm, []);
        }
        catch (Exception e)
        {
            Trace.TraceError($"[RevitDevTool] DynamoEngine: {e}");
        }
    }

    public static void Reset()
    {
        _cachedDynamoRevitInstance = null;
        _cachedAssembly = null;
        _modelStateProperty = null;
    }
}