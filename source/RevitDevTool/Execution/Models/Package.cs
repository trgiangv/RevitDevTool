namespace RevitDevTool.Execution.Models;

public enum Marketplace
{
    NuGet,
    CondaForge,
    PyPi
}

public sealed record Package(
    Marketplace Marketplace,
    string PackageId,
    string? Version,
    string? DeclaredVersion = null,
    bool IsProtected = false,
    string? LatestVersion = null,
    bool IsLatest = false);
