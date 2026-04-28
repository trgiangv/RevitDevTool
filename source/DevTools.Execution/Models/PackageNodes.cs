using System.Collections.ObjectModel;
namespace DevTools.Execution.Models;

public abstract class PackageTreeNode : TreeNodeBase
{
    public ObservableCollection<PackageTreeNode> Children { get; } = [];

    public override IEnumerable<TreeNodeBase> ChildNodes => Children;
}

public sealed class MarketplaceNode : PackageTreeNode
{
    public Marketplace Marketplace { get; }

    public MarketplaceNode(Marketplace marketplace)
    {
        Marketplace = marketplace;
        Name = marketplace switch
        {
            Marketplace.NuGet => "NuGet",
            Marketplace.CondaForge => "Conda-forge",
            _ => "PyPI"
        };
    }
}

public sealed class PackageItemNode : PackageTreeNode
{
    private Marketplace Marketplace { get; }
    private string? Version { get; }
    private string? DeclaredVersion { get; }
    private string? LatestVersion { get; }
    
    public bool IsProtected { get; }
    public bool IsLatest { get; }
    public string PackageId { get; }

    public PackageItemNode(Package package)
    {
        Marketplace = package.Marketplace;
        PackageId = package.PackageId;
        Version = package.Version;
        DeclaredVersion = package.DeclaredVersion;
        IsProtected = package.IsProtected;
        IsLatest = package.IsLatest;
        LatestVersion = package.LatestVersion;
        Name = string.IsNullOrWhiteSpace(Version) ? PackageId : $"{PackageId} ({Version})";
    }

    public Package ToRuntimePackage()
    {
        return new Package(Marketplace, PackageId, Version, DeclaredVersion, IsProtected, LatestVersion, IsLatest);
    }
}
