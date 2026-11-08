using Build.Products;

namespace Build.Pipeline;

public enum BuildTarget
{
    Ci,
    Pack,
    Release
}

/// <summary>
///     What one pipeline run builds: the product, the target, and the version stamped into its artifacts.
/// </summary>
public sealed record BuildRun(string Root, Product Product, BuildTarget Target, ProductVersion Version)
{
    public string Output => Product.Output(Root);

    public bool RunsTests => Target != BuildTarget.Pack;

    public bool BuildsArtifacts => Target != BuildTarget.Ci;
}
