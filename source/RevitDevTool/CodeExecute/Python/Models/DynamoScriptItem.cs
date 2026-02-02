using System.Diagnostics;
namespace RevitDevTool.CodeExecute.Python.Models;

public class DynamoScriptItem
{
    private string ScriptPath { get; set; } = string.Empty;

    private string ScriptTempPath { get; set; } = string.Empty;


    public DynamoScriptItem(string pythonScriptPath)
    {
        ScriptPath = pythonScriptPath;
    }

    public DynamoScriptItem()
    {
        
    }

    public string CreatePythonFile()
    {
        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Python File (*.py)|*.py",
            Title = "New Python Script",
            FileName = "Script.py"
        };

        if (saveFileDialog.ShowDialog() != true) return string.Empty;
        var filePath = saveFileDialog.FileName;
        try
        {
            System.IO.File.WriteAllText(filePath, DynamoTemplate.TemplatePythonScript);
            ScriptPath = filePath;
            return filePath;
        }
        catch (Exception ex)
        {
            Trace.TraceError($"DynamoScriptItem: Failed to create new Python file. Exception: {ex.Message}");
        }
        return string.Empty;
    }

    private string GetScriptTemplate()
    {
        if (!string.IsNullOrEmpty(ScriptPath) && System.IO.File.Exists(ScriptPath))
        {
            return DynamoTemplate.TemplateDyn
                .Replace($"{{{DynamoTemplate.TemplateName}}}", System.IO.Path.GetFileNameWithoutExtension(ScriptPath))
                .Replace($"{{{DynamoTemplate.TemplateScripPath}}}", ScriptPath.Replace(@"\", @"\\"));
        }

        Trace.TraceError($"DynamoScriptItem: Script path is invalid or file does not exist: {ScriptPath}.");
        return string.Empty;
    }

    public string Create()
    {
        if (!string.IsNullOrEmpty(ScriptTempPath) && System.IO.File.Exists(ScriptTempPath))
        {
            return ScriptTempPath;
        }

        try
        {
            var tempDynPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{System.IO.Path.GetFileNameWithoutExtension(ScriptPath)}_{Guid.NewGuid()}.dyn");
            var dynContent = GetScriptTemplate();
            if (!string.IsNullOrEmpty(dynContent))
            {
                System.IO.File.WriteAllText(tempDynPath, dynContent);
                ScriptTempPath = tempDynPath;
                Debug.WriteLine($"DynamoScriptItem: Created temp .dyn file at: {tempDynPath}");
                return tempDynPath;
            }
            else
            {
                Trace.TraceError($"DynamoScriptItem: GetScriptTemplate returned empty content for: {ScriptPath}");
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"DynamoScriptItem: Failed to create temp Dynamo file. Exception: {ex.Message}");
        }
        return string.Empty;
    }

    public void Cleanup()
    {
        try
        {
            if (string.IsNullOrEmpty(ScriptTempPath) || !System.IO.File.Exists(ScriptTempPath)) return;
            System.IO.File.Delete(ScriptTempPath);
            ScriptTempPath = string.Empty;
        }
        catch (Exception ex)
        {
            Trace.TraceError($"DynamoScriptItem: Failed to delete temp Dynamo file. Exception: {ex.Message}");
        }
    }
}