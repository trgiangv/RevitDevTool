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
    private const int DynamoModelStateRunning = 2; // DynamoModel.DynamoModelState
    private const string ModelState = "ModelState";
    private const string DynamoRevitCommandData = "Dynamo.Applications.DynamoRevitCommandData";

    private const string JournalData = "JournalData";
    private const string Application = "Application";
    private const string ExecuteCommand = "ExecuteCommand";
    private const string RevitDynamoModel = "RevitDynamoModel";
    private const string ForceRun = "ForceRun";

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

            // Cache assembly lookup
            _cachedAssembly ??= AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.FullName != null && a.FullName.Contains(DynamoRevitDs));

            if (_cachedAssembly == null)
            {
                Trace.TraceError("[RevitDevTool] DynamoEngine: DynamoRevitDS assembly not found");
                return;
            }

            // Cache DynamoRevit instance - only create once!
            _cachedDynamoRevitInstance ??= _cachedAssembly.CreateInstance(DynamoRevit);

            // Create command data (new each time - journal data changes)
            var dta = _cachedAssembly.CreateInstance(DynamoRevitCommandData);
            dta?.GetType().GetProperty(Application)?.SetValue(dta, Context.UiApplication, null);
            dta?.GetType().GetProperty(JournalData)?.SetValue(dta, journalData, null);

            // Execute
            _cachedDynamoRevitInstance?.GetType().GetMethod(ExecuteCommand)?.Invoke(_cachedDynamoRevitInstance, [dta]);

            // Force run
            var rdm = _cachedDynamoRevitInstance?.GetType().GetProperty(RevitDynamoModel)?.GetValue(_cachedDynamoRevitInstance);
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