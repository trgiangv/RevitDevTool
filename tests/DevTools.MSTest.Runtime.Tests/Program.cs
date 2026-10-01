using System.Diagnostics;
using System.Reflection;
using System.Text;
using DevTools.MSTest.Runtime;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.MSTest.Runtime.Tests;

public static class Program
{
    private static string? _fixtureSimpleName;

    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveAlreadyLoaded;
        if (args.Length == 1)
        {
            try
            {
                return RunCheck(args[0]);
            }
            catch (Exception exception)
            {
                Console.WriteLine("FAIL " + args[0]);
                Console.WriteLine(exception);
                return 1;
            }
        }

        if (args.Length != 0)
            return Fail("unexpected arguments: " + string.Join(" ", args));

        var executable = Process.GetCurrentProcess().MainModule?.FileName
            ?? Assembly.GetExecutingAssembly().Location;
        foreach (var check in new[] { "identity", "generation", "output", "selection", "cancel" })
        {
            var code = RunChild(executable, check);
            if (code != 0)
                return code;
        }

        return 0;
    }

    private static int RunChild(string executable, string check)
    {
        var start = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = check,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory,
            CreateNoWindow = true,
        };
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start " + check);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data != null)
                stdout.AppendLine(eventArgs.Data);
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data != null)
                stderr.AppendLine(eventArgs.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(180000))
        {
            try
            {
                process.Kill();
            }
            catch (Exception)
            {
            }

            Console.Write(stdout);
            Console.Write(stderr);
            return Fail(check + " timed out");
        }

        process.WaitForExit();
        Console.Write(stdout);
        if (stderr.Length > 0)
            Console.Error.Write(stderr);
        if (process.ExitCode != 0)
            Console.WriteLine("FAIL " + check + " exit " + process.ExitCode);
        return process.ExitCode;
    }

    private static int RunCheck(string name) => name switch
    {
        "identity" => Identity(),
        "generation" => Generation(),
        "output" => Output(),
        "selection" => Selection(),
        "cancel" => Cancel(),
        _ => Fail("unknown check " + name),
    };

    private static int Identity()
    {
        var path = FindFixture("IdentityA", "DevTools.MSTest.IdentityProbe");
        var assembly = LoadFixture(path);
        using var session = new MSTestRuntimeSession(assembly, path, "identity");
        var response = session.Run(Request(path, TestSelection.All), new Sink(), CancellationToken.None);
        var executed = GetField(assembly, "ExecutingAssembly") as Assembly;
        if (!ReferenceEquals(executed, assembly))
            return Fail("identity assembly mismatch " + Describe(response));
        if (!string.Equals(GetField(assembly, "Marker") as string, "A", StringComparison.Ordinal))
            return Fail("identity marker " + GetField(assembly, "Marker"));
        if (!Equals(GetField(assembly, "Executions"), 1))
            return Fail("identity executions " + GetField(assembly, "Executions"));
        if (response.Results.Count != 1 || response.Results[0].Outcome != TestOutcomes.Passed)
            return Fail("identity results " + Describe(response));
        const string expectedName =
            "DevTools.MSTest.Runtime.Tests.Fixtures.IdentityTests.MarksExecutingAssembly";
        if (!Guid.TryParse(response.Results[0].TestId, out _))
            return Fail("identity test id " + response.Results[0].TestId);
        if (!string.Equals(response.Results[0].FullName, expectedName, StringComparison.Ordinal))
            return Fail("identity full name " + response.Results[0].FullName);

        Console.WriteLine("PASS identity");
        return 0;
    }

    private static int Generation()
    {
        var pathA = FindFixture("IdentityA", "DevTools.MSTest.IdentityProbe");
        var pathB = FindFixture("IdentityB", "DevTools.MSTest.IdentityProbe");
        var assemblyA = LoadFixture(pathA);
        var assemblyB = LoadFixture(pathB);
        // Another generation registered its copy last (MTP keeps ONE process-wide hook).
        // A session must still run the copy it was created for.
        MSTestAssemblyRegistration.Register(assemblyB);
        using var session = new MSTestRuntimeSession(assemblyA, pathA, "generation");
        var response = session.Run(Request(pathA, TestSelection.All), new Sink(), CancellationToken.None);
        var executed = GetField(assemblyA, "ExecutingAssembly") as Assembly;
        if (!ReferenceEquals(executed, assemblyA))
        {
            return Fail(
                "generation executed "
                + (executed?.FullName ?? "<null>")
                + " markerA=" + GetField(assemblyA, "Marker")
                + " execA=" + GetField(assemblyA, "Executions")
                + " execB=" + GetField(assemblyB, "Executions")
                + " results " + Describe(response));
        }

        if (!string.Equals(GetField(assemblyA, "Marker") as string, "A", StringComparison.Ordinal))
            return Fail("generation marker " + GetField(assemblyA, "Marker"));
        if (!Equals(GetField(assemblyB, "Executions"), 0))
            return Fail("generation B also ran");

        Console.WriteLine("PASS generation");
        return 0;
    }
    private static int Output()
    {
        var framework = Assembly.Load(new AssemblyName("MSTest.TestFramework"));
        var before = LoggerHandlers(framework);
        var stdout = Console.Out;
        var stderr = Console.Error;

        var path = FindFixture("Output", "DevTools.MSTest.OutputProbe");
        var assembly = LoadFixture(path);
        var pane = new PaneListener();
        Trace.Listeners.Add(pane);
        try
        {
            var listeners = new TraceListener[Trace.Listeners.Count];
            Trace.Listeners.CopyTo(listeners, 0);
            using var session = new MSTestRuntimeSession(assembly, path, "output");
            var first = session.Run(Request(path, TestSelection.All), new Sink(), CancellationToken.None);
            var second = session.Run(Request(path, TestSelection.All), new Sink(), CancellationToken.None);

            if (!ReferenceEquals(Console.Out, stdout))
                return Fail("Console.Out was replaced");
            if (!ReferenceEquals(Console.Error, stderr))
                return Fail("Console.Error was replaced");
            if (Trace.Listeners.Count != listeners.Length)
                return Fail("Trace.Listeners length changed to " + Trace.Listeners.Count);
            for (var index = 0; index < listeners.Length; index++)
            {
                if (!ReferenceEquals(Trace.Listeners[index], listeners[index]))
                    return Fail("Trace.Listeners[" + index + "] was replaced");
            }

            if (OutputProblem(first) is { } firstProblem)
                return Fail(firstProblem);
            if (OutputProblem(second) is { } secondProblem)
                return Fail(secondProblem);
            if (RowOutputProblem(first) is { } rowProblem)
                return Fail(rowProblem);

            var writes = first.Results.Single(result => result.DisplayName == "Writes");
            var byId = session.Run(
                Request(path, TestSelection.FromTestIds([writes.TestId])),
                new Sink(),
                CancellationToken.None);
            if (OutputProblem(byId) is { } byIdProblem)
                return Fail("by id " + byIdProblem);
            if (pane.Count("stdout-probe") != 3 || pane.Count("stderr-probe") != 3 || pane.Count("trace-probe") != 3)
            {
                return Fail(
                    "pane stdout=" + pane.Count("stdout-probe")
                    + " stderr=" + pane.Count("stderr-probe")
                    + " trace=" + pane.Count("trace-probe")
                    + " text=" + pane.Text);
            }

            var after = LoggerHandlers(framework);
            Console.WriteLine("NOTE Logger.OnLogMessage handlers before=" + before + " after=" + after);
            if (after <= before)
                return Fail("Logger.OnLogMessage invocation list did not grow");

            Console.WriteLine("PASS output");
            return 0;
        }
        finally
        {
            Trace.Listeners.Remove(pane);
        }
    }

    private static string? OutputProblem(TestRunResponse response)
    {
        var writes = response.Results.SingleOrDefault(result => result.DisplayName == "Writes");
        if (writes is null || writes.Outcome != TestOutcomes.Passed)
            return "output results " + Describe(response);

        var output = writes.Output ?? string.Empty;
        if (Count(output, "stdout-probe") != 1 || Count(output, "stderr-probe") != 1 || Count(output, "trace-probe") != 1)
        {
            return "result stdout=" + Count(output, "stdout-probe")
                + " stderr=" + Count(output, "stderr-probe")
                + " trace=" + Count(output, "trace-probe")
                + " output=" + output;
        }

        return null;
    }

    private static string? RowOutputProblem(TestRunResponse response)
    {
        foreach (var row in new[] { ("Unit_X", "row:1.0,0.0,0.0"), ("Unit_Y", "row:0.0,1.0,0.0"), ("Unit_Z", "row:0.0,0.0,1.0") })
        {
            var result = response.Results.SingleOrDefault(item => item.DisplayName == row.Item1);
            var output = result?.Output ?? string.Empty;
            var trace = "trace-basis:" + row.Item2.Substring("row:".Length);
            if (result is null || result.Outcome != TestOutcomes.Passed
                || !output.Contains(row.Item2) || !output.Contains(trace))
                return row.Item1 + " output '" + output + "'";

            foreach (var other in new[] { "1.0,0.0,0.0", "0.0,1.0,0.0", "0.0,0.0,1.0" })
            {
                if (row.Item2.EndsWith(other, StringComparison.Ordinal))
                    continue;
                if (output.Contains("row:" + other) || output.Contains("trace-basis:" + other))
                    return row.Item1 + " also contains " + other + " output '" + output + "'";
            }
        }

        return null;
    }

    private static int Count(string text, string probe)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(probe, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += probe.Length;
        }

        return count;
    }

    private static int Selection()
    {
        const string betaName = "DevTools.MSTest.Runtime.Tests.Fixtures.SelectionTests.Beta";
        var path = FindFixture("Selection", "DevTools.MSTest.SelectionProbe");
        var assembly = LoadFixture(path);
        using var session = new MSTestRuntimeSession(assembly, path, "selection");

        Reset(assembly);
        var all = session.Run(Request(path, TestSelection.All), new Sink(), CancellationToken.None);
        if (!Equals(GetField(assembly, "Executions"), 3))
            return Fail("selection all executions " + GetField(assembly, "Executions") + " " + Describe(all));
        var beta = all.Results.SingleOrDefault(result =>
            string.Equals(result.FullName, betaName, StringComparison.Ordinal));
        if (beta is null || !Guid.TryParse(beta.TestId, out _))
            return Fail("selection all did not publish a uid for Beta " + Describe(all));

        Reset(assembly);
        var one = session.Run(
            Request(path, TestSelection.FromTestIds([beta.TestId])),
            new Sink(),
            CancellationToken.None);
        if (!Equals(GetField(assembly, "Executions"), 1)
            || !string.Equals(GetField(assembly, "Last") as string, "Beta", StringComparison.Ordinal))
        {
            return Fail(
                "selection one executions " + GetField(assembly, "Executions")
                + " last " + GetField(assembly, "Last")
                + " " + Describe(one));
        }

        var chain = all.Results
            .Where(result => result.FullName!.Contains(".ChainTests."))
            .ToDictionary(result => result.FullName!.Substring(result.FullName.LastIndexOf('.') + 1), result => result.TestId);
        if (chain.Count != 3)
            return Fail("selection chain ids " + Describe(all));

        // A run of Three by id pulls One and Two. MSTest then orders them, so Three sees Step == 2.
        Reset(assembly);
        var alone = session.Run(
            Request(path, TestSelection.FromTestIds([chain["Three"]])),
            new Sink(),
            CancellationToken.None);
        if (alone.Results.Count != 3
            || alone.Results.Any(result => result.Outcome != TestOutcomes.Passed)
            || !Equals(GetField(assembly, "Step"), 2))
        {
            return Fail(
                "selection chain by id step=" + GetField(assembly, "Step") + " " + Describe(alone));
        }

        var three = alone.Results.Single(result => result.FullName!.EndsWith(".Three", StringComparison.Ordinal));
        if (three.Output is null || !three.Output.Contains("chain-step=2"))
        {
            return Fail(
                "selection chain stdout on Three was '" + three.Output
                + "' others="
                + string.Join(" | ", alone.Results.Select(result => result.FullName + "=" + result.Output)));
        }

        Reset(assembly);
        var withPrerequisites = session.Run(
            Request(path, TestSelection.FromTestIds([chain["Three"], chain["Two"], chain["One"]])),
            new Sink(),
            CancellationToken.None);
        if (withPrerequisites.Results.Count != 3
            || withPrerequisites.Results.Any(result => result.Outcome != TestOutcomes.Passed))
        {
            return Fail("selection chain with prerequisites " + Describe(withPrerequisites));
        }

        Reset(assembly);
        var empty = session.Run(
            Request(path, TestSelection.FromTestIds([])),
            new Sink(),
            CancellationToken.None);
        if (!Equals(GetField(assembly, "Executions"), 0) || empty.Results.Count != 0 || empty.DiagnosticCode != null)
            return Fail("selection empty " + GetField(assembly, "Executions") + " " + Describe(empty));

        Reset(assembly);
        var filter = session.Run(
            Request(path, TestSelection.FromFrameworkFilter("filter-xml", "<filter />")),
            new Sink(),
            CancellationToken.None);
        if (filter.DiagnosticCode != "testing/invalid_request" || !Equals(GetField(assembly, "Executions"), 0))
            return Fail("selection filter " + filter.DiagnosticCode + " executions " + GetField(assembly, "Executions"));

        var names = session.Run(
            Request(path, TestSelection.FromNames(["Beta"])),
            new Sink(),
            CancellationToken.None);
        if (names.DiagnosticCode != "testing/invalid_request")
            return Fail("selection names " + names.DiagnosticCode);

        Console.WriteLine("PASS selection");
        return 0;
    }

    private static int Cancel()
    {
        var path = FindFixture("Cancel", "DevTools.MSTest.CancelProbe");
        var assembly = LoadFixture(path);
        using var session = new MSTestRuntimeSession(assembly, path, "cancel");
        var runId = Guid.NewGuid();
        var request = new TestRunRequest(
            1,
            runId,
            TestFrameworkId.NUnit,
            new TestAssemblyReference(path),
            TestSelection.All);
        var run = Task.Run(() => session.Run(request, new Sink(), CancellationToken.None));
        var started = SpinWait.SpinUntil(
            () => Equals(ReadStarted(assembly), 1),
            TimeSpan.FromSeconds(20));
        if (!started)
        {
            return Fail(
                "cancel test did not start. completed=" + run.IsCompleted
                + " " + (run.IsFaulted ? run.Exception?.GetBaseException().Message : string.Empty));
        }

        session.Cancel(runId);
        if (!run.Wait(TimeSpan.FromSeconds(10)))
            return Fail("cancel did not return within 10s finished=" + GetField(assembly, "Finished"));
        if (run.IsFaulted)
            return Fail(run.Exception?.GetBaseException().ToString() ?? "cancel faulted");
        if (Equals(GetField(assembly, "Finished"), 1))
            return Fail("cancel sleep ran to completion");

        var response = run.Result;
        if (!response.Results.Any(result => result.Outcome == TestOutcomes.Cancelled))
            return Fail("expected cancelled TestCaseResult " + Describe(response));

        Console.WriteLine("PASS cancel");
        return 0;
    }

    private static int ReadStarted(Assembly assembly)
    {
        try
        {
            return GetField(assembly, "Started") is int started ? started : 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static void Reset(Assembly assembly)
    {
        SetField(assembly, "Executions", 0);
        SetField(assembly, "Last", null);
        SetField(assembly, "Step", 0);
    }

    private static TestRunRequest Request(string path, TestSelection selection) =>
        new(1, Guid.NewGuid(), TestFrameworkId.NUnit, new TestAssemblyReference(path), selection);

    private static Assembly LoadFixture(string path)
    {
        var simpleName = AssemblyName.GetAssemblyName(path).Name;
        if (_fixtureSimpleName is not null
            && !string.Equals(_fixtureSimpleName, simpleName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A child process can load one fixture name. Have " + _fixtureSimpleName + " and " + simpleName);
        }

        _fixtureSimpleName = simpleName;
        return Assembly.LoadFile(path);
    }

    private static Assembly? ResolveAlreadyLoaded(object? sender, ResolveEventArgs args)
    {
        var requested = new AssemblyName(args.Name).Name;
        if (string.Equals(requested, _fixtureSimpleName, StringComparison.Ordinal))
            return null;

        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(loaded.GetName().Name, requested, StringComparison.Ordinal))
                return loaded;
        }

        return null;
    }

    private static string FindFixture(string projectDirectory, string assemblyName)
    {
        var root = FindProjectRoot();
        var configuration = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).Parent?.Name ?? "Debug";
        var candidate = Path.Combine(
            root,
            "Fixtures",
            projectDirectory,
            "bin",
            configuration,
            "net48",
            assemblyName + ".dll");
        if (File.Exists(candidate))
            return candidate;

        var bin = Path.Combine(root, "Fixtures", projectDirectory, "bin");
        if (Directory.Exists(bin))
        {
            var matches = Directory.GetFiles(bin, assemblyName + ".dll", SearchOption.AllDirectories);
            if (matches.Length > 0)
                return matches.OrderByDescending(File.GetLastWriteTimeUtc).First();
        }

        throw new FileNotFoundException("Fixture assembly was not built.", candidate);
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DevTools.MSTest.Runtime.Tests.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate DevTools.MSTest.Runtime.Tests.csproj.");
    }

    private static object? GetField(Assembly assembly, string name)
    {
        var type = assembly.GetType("DevTools.MSTest.Runtime.Tests.Fixtures.Probe", throwOnError: true)
            ?? throw new MissingMemberException("Probe");
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingFieldException(type.FullName, name);
        return field.GetValue(null);
    }

    private static void SetField(Assembly assembly, string name, object? value)
    {
        var type = assembly.GetType("DevTools.MSTest.Runtime.Tests.Fixtures.Probe", throwOnError: true)
            ?? throw new MissingMemberException("Probe");
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingFieldException(type.FullName, name);
        field.SetValue(null, value);
    }

    private static int LoggerHandlers(Assembly framework)
    {
        var logger = framework.GetType("Microsoft.VisualStudio.TestTools.UnitTesting.Logging.Logger", throwOnError: true)
            ?? throw new MissingMemberException("Logger");
        var field = logger.GetField("OnLogMessage", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field?.GetValue(null) is not Delegate handlers)
            return 0;
        return handlers.GetInvocationList().Length;
    }

    private static string Describe(TestRunResponse response)
    {
        var results = string.Join(
            ",",
            response.Results.Select(result => result.Outcome + ":" + result.DisplayName + ":" + result.Message));
        return "code=" + response.DiagnosticCode + " message=" + response.DiagnosticMessage + " [" + results + "]";
    }

    private static int Fail(string message)
    {
        Console.WriteLine("FAIL " + message);
        return 1;
    }

    private sealed class Sink : ITestEventSink
    {
        public void Publish(TestEvent testingEvent)
        {
        }
    }

    private sealed class PaneListener : TraceListener
    {
        private readonly StringBuilder _buffer = new();

        public string Text => _buffer.ToString();

        public override void Write(string? message)
        {
            if (!string.IsNullOrEmpty(message))
                _buffer.Append(message);
        }

        public override void WriteLine(string? message) =>
            Write(string.IsNullOrEmpty(message) ? Environment.NewLine : message + Environment.NewLine);

        public int Count(string probe)
        {
            var text = Text;
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(probe, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += probe.Length;
            }

            return count;
        }
    }
}
