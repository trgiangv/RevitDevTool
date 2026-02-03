using System.Diagnostics;
namespace RevitDevTool.CodeExecute.Python.Models;

public class DynamoScriptItem
{
    private string PythonScriptPath { get; set; } = string.Empty;

    private string? DynamoScriptTempPath { get; set; }

    private string? PythonScriptTempPath { get; set; }

    public DynamoScriptItem(string pythonScriptPath)
    {
        PythonScriptPath = pythonScriptPath;
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
            PythonScriptPath = filePath;
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
        if (!string.IsNullOrEmpty(PythonScriptTempPath) && System.IO.File.Exists(PythonScriptTempPath))
        {
            return DynamoTemplate.TemplateDyn
                .Replace($"{{{DynamoTemplate.TemplateName}}}", System.IO.Path.GetFileNameWithoutExtension(PythonScriptPath))
                .Replace($"{{{DynamoTemplate.TemplateScripPath}}}", PythonScriptTempPath!.Replace(@"\", @"\\"));
        }

        Trace.TraceError($"DynamoScriptItem: Script path is invalid or file does not exist: {PythonScriptPath}.");
        return string.Empty;
    }

    public string Create()
    {
        CreateTempPythonFile();

        if (!string.IsNullOrEmpty(DynamoScriptTempPath) && System.IO.File.Exists(DynamoScriptTempPath))
        {
            return DynamoScriptTempPath!;
        }

        try
        {
            var tempDynPath = DynamoScriptTempPath ??= System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{System.IO.Path.GetFileNameWithoutExtension(PythonScriptPath)}_{Guid.NewGuid()}.dyn");
            var dynContent = GetScriptTemplate();
            if (!string.IsNullOrEmpty(dynContent))
            {
                System.IO.File.WriteAllText(tempDynPath, dynContent);
                Debug.WriteLine($"DynamoScriptItem: Created temp .dyn file at: {tempDynPath}");
                return tempDynPath;
            }
            Trace.TraceError($"DynamoScriptItem: GetScriptTemplate returned empty content for: {PythonScriptPath}");
        }
        catch (Exception ex)
        {
            Trace.TraceError($"DynamoScriptItem: Failed to create temp Dynamo file. Exception: {ex.Message}");
        }
        return string.Empty;
    }

    private void CreateTempPythonFile()
    {
        if (string.IsNullOrEmpty(PythonScriptPath) || !System.IO.File.Exists(PythonScriptPath))
        {
            Trace.TraceError($"DynamoScriptItem: Script path is invalid or file does not exist: {PythonScriptPath}.");
            return;
        }

        try
        {
            var scriptContent = System.IO.File.ReadAllText(PythonScriptPath);
            var tempScriptPath = PythonScriptTempPath ??= System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{System.IO.Path.GetFileNameWithoutExtension(PythonScriptPath)}_{Guid.NewGuid()}.py");
            
            var modifiedContent = $"__file__ = r\"{PythonScriptPath}\"\n{scriptContent}";
            System.IO.File.WriteAllText(tempScriptPath, modifiedContent);
            
            Debug.WriteLine($"DynamoScriptItem: Created temp script file with __file__ at: {tempScriptPath}");
        }
        catch (Exception ex)
        {
            Trace.TraceError($"DynamoScriptItem: Failed to create temp script file with path. Exception: {ex.Message}");
        }
    }

    public void Cleanup()
    {
        try
        {
            if (!string.IsNullOrEmpty(DynamoScriptTempPath) && System.IO.File.Exists(DynamoScriptTempPath)) 
            {
                System.IO.File.Delete(DynamoScriptTempPath!);
                DynamoScriptTempPath = null;
            }
            if (!string.IsNullOrEmpty(PythonScriptTempPath) && System.IO.File.Exists(PythonScriptTempPath)) 
            {
                System.IO.File.Delete(PythonScriptTempPath!);
                PythonScriptTempPath = null;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"DynamoScriptItem: Failed to delete temp Dynamo file. Exception: {ex.Message}");
        }
    }
}