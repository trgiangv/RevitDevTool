using Build.Products;

namespace Build.Pipeline;

[Flags]
public enum Stage
{
    Compile = 1,
    Test = 2,
    Pack = 4,
    Release = Compile | Test | Pack
}

/// <summary>
///     What one pipeline run builds: the product, its stages, and the version stamped into its artifacts.
///     <see cref="HostVersion"/> limits the add-in compile to one host version.
/// </summary>
public sealed record BuildRun(string Root, Product Product, Stage Stages, ProductVersion Version, string? HostVersion = null)
{
    public string Output => Product.Output(Root);

    public bool Has(Stage stage) => Stages.HasFlag(stage);

    public bool IsRelease => Stages == Stage.Release;
}
