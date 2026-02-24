using RevitDevTool.Bridge;
using RevitDevTool.Bridge.Enums.Revit;
using RevitDevTool.Bridge.Revit;

namespace RevitDevTool.Console.Services;

public static class BatchConfigResolver
{
    public static List<ResolvedJob> Resolve(BatchConfig config)
    {
        return config.Files.Select(file => ResolveFile(file, config.Defaults)).ToList();
    }

    private static ResolvedJob ResolveFile(FileEntry file, JobDefaults defaults)
    {
        return new ResolvedJob
        {
            FilePath = file.Path,
            HostVersion = ResolveHostVersion(file, defaults),
            Script = ResolveScript(file, defaults),
            Open = ResolveOpenOptions(file, defaults),
            Lifecycle = ResolveLifecycle(file, defaults)
        };
    }

    private static string ResolveHostVersion(FileEntry file, JobDefaults defaults)
    {
        var version = file.HostVersion ?? defaults.HostVersion;

        if (version == null)
        {
            try
            {
                var fileInfo = new RevitFileInfo.RevitFileInfo(file.Path);
                var year = fileInfo.GetRevitYear();
                if (year != null)
                    version = year.Value.ToString();
            }
            catch
            {
                // File might not exist yet or be unreadable
            }
        }

        return version ?? throw new InvalidOperationException(
            $"Cannot determine host version for '{file.Path}'. " +
            "Specify hostVersion in file entry or defaults.");
    }

    private static string ResolveScript(FileEntry file, JobDefaults defaults)
    {
        return file.Script
               ?? defaults.Script
               ?? throw new InvalidOperationException(
                   $"No script specified for '{file.Path}'. " +
                   "Specify script in file entry or defaults.");
    }

    private static RevitOpenOptions ResolveOpenOptions(FileEntry file, JobDefaults defaults)
    {
        return new RevitOpenOptions
        {
            Headless = Resolve(file.Headless, defaults.Headless, true),
            Audit = Resolve(file.Audit, defaults.Audit, false),
            DetachFromCentral = Resolve(file.DetachFromCentral, defaults.DetachFromCentral, CentralMode.DetachAndPreserveWorksets),
            Workset = Resolve(file.Workset, defaults.Workset, WorksetMode.OpenAllWorksets),
            AllowOpeningLocalByWrongUser = Resolve(file.AllowOpeningLocalByWrongUser, defaults.AllowOpeningLocalByWrongUser, true),
            IgnoreExtensibleStorageSchemaConflict = Resolve(file.IgnoreExtensibleStorageSchemaConflict, defaults.IgnoreExtensibleStorageSchemaConflict, true),
            OpenWorksets = file.OpenWorksets ?? defaults.OpenWorksets ?? [],
            CloseWorksets = file.CloseWorksets ?? defaults.CloseWorksets ?? []
        };
    }

    private static LifecyclePolicy ResolveLifecycle(FileEntry file, JobDefaults defaults)
    {
        return new LifecyclePolicy
        {
            CloseDocument = Resolve(file.CloseDocument, defaults.CloseDocument, true),
            CloseHost = Resolve(file.CloseHost, defaults.CloseHost, false)
        };
    }

    private static T Resolve<T>(T? fileValue, T? defaultValue, T fallback) where T : struct
    {
        return fileValue ?? defaultValue ?? fallback;
    }
}
