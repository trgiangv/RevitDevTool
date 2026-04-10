using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Python.Runtime;
using RevitDevTool.Execution.Providers.Python;

namespace RevitDevTool.ExternalExecution.Testing;

public sealed class TestExecutionService(PythonInitializer pythonInitializer)
{
    public static bool TryParseRequest(JsonElement? @params, out TestExecutionRequest? request, out string? error)
    {
        string? moduleSource = null;
        string? testName = null;
        string? filePath = null;
        string? className = null;

        if (@params?.TryGetProperty(PythonScopeVars.ModuleSource, out var sourceElement) == true)
            moduleSource = sourceElement.GetString();
        if (@params?.TryGetProperty(PythonScopeVars.TestName, out var testNameElement) == true)
            testName = testNameElement.GetString();
        if (@params?.TryGetProperty(PythonScopeVars.TestFilePath, out var filePathElement) == true)
            filePath = filePathElement.GetString();
        if (@params?.TryGetProperty(PythonScopeVars.ClassName, out var classNameElement) == true)
            className = classNameElement.GetString();

        if (string.IsNullOrWhiteSpace(moduleSource) || string.IsNullOrWhiteSpace(testName))
        {
            request = null;
            error = "module_source and test_name are required.";
            return false;
        }

        var resolvedModuleSource = moduleSource!;
        var resolvedTestName = testName!;
        request = new TestExecutionRequest(resolvedModuleSource, resolvedTestName, filePath ?? "<pytest>", className);
        error = null;
        return true;
    }

    public TestExecutionResponse Execute(TestExecutionRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var stdout = new System.IO.StringWriter();
        string outcome;
        string message;
        string traceback;

        try
        {
            using (Py.GIL())
            {
                if (pythonInitializer.GlobalScope is null)
                    throw new InvalidOperationException("Python runtime not initialized.");

                using var scope = pythonInitializer.GlobalScope.NewScope();
                var rootFolder = System.IO.Path.GetDirectoryName(request.FilePath) ?? string.Empty;
                PythonExecutor.PrepareExecutionScope(scope, request.FilePath, rootFolder);

                scope.Set(PythonScopeVars.Source, new PyString(request.ModuleSource));
                scope.Set(PythonScopeVars.TestInvoke, new PyString(BuildTestInvokeCode(request.TestName, request.ClassName)));

                scope.Exec("""
                           import io, sys, traceback as _tb
                           _captured = io.StringIO()
                           _old_stdout = sys.stdout
                           sys.stdout = _captured
                           _test_outcome = "passed"
                           _test_message = ""
                           _test_traceback = ""
                           try:
                               compiled_code = compile(__source__, __file__, 'exec')
                               exec(compiled_code, globals())
                               exec(compile(__test_invoke__, '<test_invoke>', 'exec'), globals())
                           except AssertionError as e:
                               _test_outcome = "failed"
                               _test_message = str(e)
                               _test_traceback = _tb.format_exc()
                           except Exception as e:
                               _test_outcome = "error"
                               _test_message = str(e)
                               _test_traceback = _tb.format_exc()
                           finally:
                               sys.stdout = _old_stdout
                           """);

                outcome = scope.Get("_test_outcome")?.As<string>() ?? "error";
                message = scope.Get("_test_message")?.As<string>() ?? string.Empty;
                traceback = scope.Get("_test_traceback")?.As<string>() ?? string.Empty;
                var captured = scope.Get("_captured");
                stdout.Write(captured?.InvokeMethod("getvalue")?.As<string>() ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            outcome = "error";
            message = ex.Message;
            traceback = ex.ToString();
        }

        stopwatch.Stop();
        return new TestExecutionResponse(outcome, message, traceback, stdout.ToString(), stopwatch.Elapsed.TotalMilliseconds);
    }

    private static string BuildTestInvokeCode(string testName, string? className)
    {
        return string.IsNullOrEmpty(className)
            ? $"{testName}()"
            : $"_test_instance = {className}()\n_test_instance.{testName}()";
    }
}

public sealed record TestExecutionRequest(
    string ModuleSource,
    string TestName,
    string FilePath,
    string? ClassName);

public sealed record TestExecutionResponse(
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("traceback")] string Traceback,
    [property: JsonPropertyName("stdout")] string Stdout,
    [property: JsonPropertyName("duration_ms")] double DurationMs);
