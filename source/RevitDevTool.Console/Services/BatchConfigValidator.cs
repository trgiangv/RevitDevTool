using RevitDevTool.Bridge;
using RevitDevTool.Bridge.Enums;

namespace RevitDevTool.Console.Services;

public static class BatchConfigValidator
{
    public static void Validate(List<ResolvedJob> jobs, ProcessingMode mode)
    {
        if (jobs.Count == 0)
            throw new InvalidOperationException("No jobs to process.");

        foreach (var job in jobs)
        {
            if (string.IsNullOrWhiteSpace(job.Script))
                throw new InvalidOperationException($"Job for '{job.FilePath}' has no script.");

            ValidateRevitVersionCompatibility(job);
        }

        if (mode == ProcessingMode.SequentialSingle)
        {
            var versions = jobs.Select(j => j.HostVersion).Distinct().ToList();
            if (versions.Count > 1)
            {
                throw new InvalidOperationException(
                    $"SequentialSingle mode requires all files to target the same version, " +
                    $"but found: {string.Join(", ", versions)}");
            }
        }
    }

    private static void ValidateRevitVersionCompatibility(ResolvedJob job)
    {
        if (string.IsNullOrEmpty(job.FilePath) || !File.Exists(job.FilePath))
            return;

        if (!int.TryParse(job.HostVersion, out var targetYear))
            return;

        try
        {
            var fileInfo = new RevitFileInfo.RevitFileInfo(job.FilePath);
            var fileVersion = fileInfo.GetRevitYear();
            if (fileVersion != null && targetYear < fileVersion)
            {
                throw new InvalidOperationException(
                    $"File '{job.FilePath}' requires version {fileVersion} or higher, " +
                    $"but target is {job.HostVersion}.");
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            // File unreadable for version check - skip validation
        }
    }
}
