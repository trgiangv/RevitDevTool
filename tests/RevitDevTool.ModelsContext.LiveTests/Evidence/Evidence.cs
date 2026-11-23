using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed class ImageRef
{
    public string Label { get; set; } = string.Empty;

    public string File { get; set; } = string.Empty;
}

internal sealed class CaseRecord
{
    public string Layer { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string Test { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Expected { get; set; } = string.Empty;

    public string? Oracle { get; set; }

    public string? Panel { get; set; }

    public string Outcome { get; set; } = "RUNNING";

    public string? Message { get; set; }

    public long TotalMs { get; set; }

    public List<string> Steps { get; set; } = [];

    public Dictionary<string, long> TimingsMs { get; set; } = [];

    public List<ImageRef> Images { get; set; } = [];

    public List<string> RequiredImages { get; set; } = [];

    public List<string> ManualSteps { get; set; } = [];

    public string? Safety { get; set; }
}

internal sealed class LayerRun
{
    public string Layer { get; set; } = string.Empty;

    public int HostProcessId { get; set; }

    public DateTime Started { get; set; }

    public DateTime Ended { get; set; }

    public long WallMs { get; set; }

    public List<CaseRecord> Cases { get; set; } = [];
}

internal static class Evidence
{
    public const string Pass = "PASS";
    public const string Fail = "FAIL";
    public const string Inconclusive = "INCONCLUSIVE";

    private const string RootName = "models-context-tests";
    private const string LatestFile = "latest.txt";
    private const string ImageFolder = "images";
    private const string ImagesFile = "images.on";

    private static readonly object Gate = new();
    private static string? _folder;
    private static LayerRun? _run;
    private static CaseRecord? _current;
    private static Stopwatch _caseWatch = new();

    public static string Layer { get; set; } = "L2";

    public static int HostProcessId { get; set; } = Process.GetCurrentProcess().Id;

    public static string Folder
    {
        get
        {
            lock (Gate)
            {
                return _folder ??= OpenFolder();
            }
        }
    }

    public static CaseRecord? Current => _current;

    public static void StartSession()
    {
        lock (Gate)
        {
            _folder = null;
            _run = null;
            _current = null;
        }
    }

    public static bool Images => File.Exists(Path.Combine(Root(), ImagesFile));

    public static CaseRecord Begin(CaseItem item, string test)
    {
        return Begin(item.Id, item.Description, item.ExpectedText, item.ManualSteps, item.Images, test);
    }

    public static CaseRecord Begin(string id, string description, string expected, IEnumerable<string> manualSteps,
        IEnumerable<string> images, string test)
    {
        lock (Gate)
        {
            _run ??= new LayerRun { Layer = Layer, HostProcessId = HostProcessId, Started = DateTime.Now };
            _current = new CaseRecord
            {
                Layer = Layer,
                Id = id,
                Test = test,
                Description = description,
                Expected = expected,
                ManualSteps = [.. manualSteps],
                RequiredImages = Layer == "L2" && !Images ? [] : [.. images]
            };
            _caseWatch = Stopwatch.StartNew();
            return _current;
        }
    }

    public static void Run(CaseItem item, string test, Action body, Action<string> fail)
    {
        Begin(item, test);
        try
        {
            body();
            if (MissingImages() is { } missing)
            {
                fail(missing);
            }

            End(Pass, null);
        }
        catch (Exception exception)
        {
            End(exception.GetType().Name.Contains(Inconclusive, StringComparison.OrdinalIgnoreCase) ? Inconclusive : Fail, exception.Message);
            throw;
        }
    }

    public static int? HostOf(string layer)
    {
        var path = Path.Combine(Folder, $"results-{layer}.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<LayerRun>(File.ReadAllText(path), CaseFiles.Options)?.HostProcessId : null;
    }

    public static void Step(string text)
    {
        _current?.Steps.Add(text);
    }

    public static void Safety(string text)
    {
        if (_current is { } record)
        {
            record.Safety = text;
        }
    }

    public static string? LatestFolder()
    {
        var latest = Path.Combine(Root(), LatestFile);
        if (!File.Exists(latest))
        {
            return null;
        }

        var folder = Path.Combine(Root(), File.ReadAllText(latest).Trim());
        return Directory.Exists(folder) ? folder : null;
    }

    public static string WorkFolder(string year)
    {
        return Path.Combine(RepositoryRoot(), "artifacts", "models-context-work", year);
    }

    public static T Time<T>(string name, Func<T> work)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            return work();
        }
        finally
        {
            Timing(name, watch.ElapsedMilliseconds);
        }
    }

    public static void Time(string name, Action work)
    {
        Time(name, () =>
        {
            work();
            return 0;
        });
    }

    public static void Timing(string name, long milliseconds)
    {
        if (_current is { } record)
        {
            record.TimingsMs[name] = record.TimingsMs.TryGetValue(name, out var before) ? before + milliseconds : milliseconds;
        }
    }

    public static void Values(object? oracle, object? panel)
    {
        if (_current is not { } record)
        {
            return;
        }

        record.Oracle = oracle is null ? record.Oracle : Text(oracle);
        record.Panel = panel is null ? record.Panel : Text(panel);
    }

    public static void Image(string label, Action<string> capture)
    {
        if (_current is not { } record)
        {
            return;
        }

        var name = Safe($"{record.Layer}-{record.Id}-{record.Test}-{label}") + ".png";
        var path = Path.Combine(Folder, ImageFolder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            capture(path);
            if (File.Exists(path))
            {
                record.Images.Add(new ImageRef { Label = label, File = ImageFolder + "/" + name });
            }
        }
        catch (Exception exception)
        {
            record.Steps.Add($"Image '{label}' was not captured: {exception.Message}");
        }
    }

    public static string? MissingImages()
    {
        if (_current is not { } record)
        {
            return null;
        }

        var missing = record.RequiredImages.Where(label => record.Images.All(image => image.Label != label)).ToList();
        return missing.Count == 0 ? null : $"{record.Id}: images not captured: {string.Join(", ", missing)}.";
    }

    public static void End(string outcome, string? message)
    {
        lock (Gate)
        {
            if (_current is not { } record || _run is null)
            {
                return;
            }

            record.Outcome = outcome;
            record.Message = message;
            record.TotalMs = _caseWatch.ElapsedMilliseconds;
            _run.Cases.Add(record);
            _run.Ended = DateTime.Now;
            _run.WallMs = (long)(_run.Ended - _run.Started).TotalMilliseconds;
            _current = null;
            Flush(_run);
        }
    }

    private static string Text(object value)
    {
        return value switch
        {
            string text => text,
            int number => number.ToString("N0", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string Safe(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(character => invalid.Contains(character) || character == ' ' ? '_' : character).ToArray());
    }

    private static void Flush(LayerRun run)
    {
        var folder = Folder;
        File.WriteAllText(Path.Combine(folder, $"results-{run.Layer}.json"), JsonSerializer.Serialize(run, CaseFiles.Options));
        var runs = Directory.GetFiles(folder, "results-L*.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => JsonSerializer.Deserialize<LayerRun>(File.ReadAllText(path), CaseFiles.Options)!)
            .ToList();
        File.WriteAllText(Path.Combine(folder, "results.json"), JsonSerializer.Serialize(runs, CaseFiles.Options));
        File.WriteAllText(Path.Combine(folder, "report.md"), EvidenceReport.Render(Path.GetFileName(folder), runs));
    }

    private static string OpenFolder()
    {
        var root = Root();
        var latest = Path.Combine(root, LatestFile);
        if (Layer != "L2" && File.Exists(latest))
        {
            var folder = Path.Combine(root, File.ReadAllText(latest).Trim());
            if (Directory.Exists(folder) && !File.Exists(Path.Combine(folder, $"results-{Layer}.json")))
            {
                return folder;
            }
        }

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var created = Path.Combine(root, stamp);
        Directory.CreateDirectory(created);
        File.WriteAllText(latest, stamp);
        return created;
    }

    private static string Root()
    {
        var root = Path.Combine(RepositoryRoot(), "artifacts", RootName);
        Directory.CreateDirectory(root);
        return root;
    }

    private static string RepositoryRoot([CallerFilePath] string source = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RevitDevTool.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}

internal static class EvidenceReport
{
    public static string Render(string stamp, IReadOnlyList<LayerRun> runs)
    {
        var text = new StringBuilder();
        text.AppendLine($"# Model Context test evidence {stamp}");
        text.AppendLine();
        text.AppendLine($"Model: {CaseFiles.Facts.HostTitle}.rvt with the {CaseFiles.Facts.LinkTitle} link. ");
        text.AppendLine("Pass or fail comes from the assertion against the oracle (an independent plain-collector count) or the Revit API state; images are evidence only. Every case ends with \"How to verify by hand\".");
        text.AppendLine();
        text.AppendLine("## Summary");
        text.AppendLine();
        text.AppendLine("| Layer | Cases | PASS | FAIL | Other | Started | Wall time |");
        text.AppendLine("|-------|------:|-----:|-----:|------:|---------|----------:|");
        foreach (var run in runs)
        {
            var pass = run.Cases.Count(item => item.Outcome == Evidence.Pass);
            var fail = run.Cases.Count(item => item.Outcome == Evidence.Fail);
            text.AppendLine($"| {run.Layer} | {run.Cases.Count} | {pass} | {fail} | {run.Cases.Count - pass - fail} | {run.Started:yyyy-MM-dd HH:mm:ss} | {Seconds(run.WallMs)} |");
        }

        var failures = runs.SelectMany(run => run.Cases).Where(item => item.Outcome != Evidence.Pass).ToList();
        if (failures.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("## Not passed");
            text.AppendLine();
            foreach (var item in failures)
            {
                text.AppendLine($"- [{item.Layer} {item.Id} {item.Test}](#{Anchor(item)}): {item.Outcome}. {Cell(item.Message)}");
            }
        }

        foreach (var run in runs)
        {
            text.AppendLine();
            text.AppendLine($"## {run.Layer} cases");
            text.AppendLine();
            text.AppendLine("| ID | Test | Expected | Oracle | Panel | ms | Result |");
            text.AppendLine("|----|------|----------|--------|-------|---:|--------|");
            foreach (var item in run.Cases)
            {
                text.AppendLine($"| [{item.Id}](#{Anchor(item)}) | {item.Test} | {Cell(item.Expected)} | {Cell(item.Oracle)} | {Cell(item.Panel)} | {item.TotalMs} | {item.Outcome} |");
            }
        }

        text.AppendLine();
        text.AppendLine("## Case details");
        foreach (var item in runs.SelectMany(run => run.Cases))
        {
            Detail(text, item);
        }

        return text.ToString();
    }

    private static void Detail(StringBuilder text, CaseRecord item)
    {
        text.AppendLine();
        text.AppendLine($"<a id=\"{Anchor(item)}\"></a>");
        text.AppendLine($"### {item.Layer} {item.Id} {item.Test}: {item.Outcome}");
        text.AppendLine();
        text.AppendLine(item.Description);
        if (item.Message is { Length: > 0 } message)
        {
            text.AppendLine();
            text.AppendLine($"> {message.Replace("\n", " ")}");
        }

        text.AppendLine();
        text.AppendLine("Steps done:");
        text.AppendLine();
        for (var index = 0; index < item.Steps.Count; index++)
        {
            text.AppendLine($"{index + 1}. {item.Steps[index]}");
        }

        text.AppendLine();
        text.AppendLine($"- Expected: {Show(item.Expected)}");
        text.AppendLine($"- Oracle: {Show(item.Oracle)}");
        text.AppendLine($"- Panel: {Show(item.Panel)}");
        text.AppendLine($"- Timings: {string.Join(", ", item.TimingsMs.Select(pair => $"{pair.Key} {pair.Value} ms").Append($"total {item.TotalMs} ms"))}");
        if (item.Safety is { Length: > 0 } safety)
        {
            text.AppendLine($"- Safety: {safety}");
        }
        if (item.Images.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Images:");
            text.AppendLine();
            foreach (var image in item.Images)
            {
                text.AppendLine($"- {image.Label}: [{image.File}]({image.File})");
                text.AppendLine();
                text.AppendLine($"  ![{item.Id} {image.Label}]({image.File})");
                text.AppendLine();
            }
        }

        text.AppendLine();
        text.AppendLine("How to verify by hand:");
        text.AppendLine();
        for (var index = 0; index < item.ManualSteps.Count; index++)
        {
            text.AppendLine($"{index + 1}. {item.ManualSteps[index]}");
        }
    }

    private static string Anchor(CaseRecord item)
    {
        return $"{item.Layer}-{item.Id}-{item.Test}".ToLowerInvariant();
    }

    private static string Show(string? value)
    {
        return string.IsNullOrEmpty(value) ? "(not read)" : value!;
    }

    private static string Cell(string? value)
    {
        return string.IsNullOrEmpty(value) ? "" : value!.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }

    private static string Seconds(long milliseconds)
    {
        return (milliseconds / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s";
    }
}
