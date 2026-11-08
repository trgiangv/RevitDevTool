using Build.Trains;

namespace Build;

public enum BuildTarget
{
    Ci,
    Pack,
    Release
}

public sealed record BuildRun(string Root, Train Train, BuildTarget Target, TrainVersion Version)
{
    public string Output => Path.Combine(Root, "output", Train.Name);

    public bool BuildsArtifacts => Target != BuildTarget.Ci;
}
