using Build.Trains;

namespace Build.Pipeline;

public enum BuildTarget
{
    Ci,
    Pack,
    Release
}

/// <summary>
///     What one pipeline run builds: the train, the target, and the version stamped into its artifacts.
/// </summary>
public sealed record BuildRun(string Root, Train Train, BuildTarget Target, TrainVersion Version)
{
    public string Output => Train.Output(Root);

    public bool RunsTests => Target != BuildTarget.Pack;

    public bool BuildsArtifacts => Target != BuildTarget.Ci;
}
