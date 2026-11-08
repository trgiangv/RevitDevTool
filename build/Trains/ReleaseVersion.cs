using System.Text.RegularExpressions;

namespace Build.Trains;

public enum ReleaseLabel
{
    Alpha,
    Beta,
    Rc
}

public sealed partial record ReleaseVersion(int Major, int Minor, int Patch, ReleaseLabel? Label, int Number) : IComparable<ReleaseVersion>
{
    public bool IsPrerelease => Label is not null;

    public (int, int, int) Core => (Major, Minor, Patch);

    public static string Describe(VersionGrammar grammar) => grammar switch
    {
        VersionGrammar.SemVer => "MAJOR.MINOR.PATCH[-alpha.N|-beta.N|-rc.N]",
        _ => "MAJOR.MINOR.PATCH[aN|bN|rcN]"
    };

    public static ReleaseVersion? Parse(string value, VersionGrammar grammar)
    {
        var match = (grammar == VersionGrammar.SemVer ? SemVerRegex() : Pep440Regex()).Match(value);
        if (!match.Success) return null;

        ReleaseLabel? label = match.Groups["label"].Value switch
        {
            "" => null,
            "alpha" or "a" => ReleaseLabel.Alpha,
            "beta" or "b" => ReleaseLabel.Beta,
            _ => ReleaseLabel.Rc
        };

        return new ReleaseVersion(
            int.Parse(match.Groups["major"].Value),
            int.Parse(match.Groups["minor"].Value),
            int.Parse(match.Groups["patch"].Value),
            label,
            label is null ? 0 : int.Parse(match.Groups["number"].Value));
    }

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null) return 1;

        var core = Core.CompareTo(other.Core);
        if (core != 0) return core;
        if (Label is null || other.Label is null) return (Label is null).CompareTo(other.Label is null);

        var label = Label.Value.CompareTo(other.Label.Value);
        return label != 0 ? label : Number.CompareTo(other.Number);
    }

    public bool RaisesMajorFrom(ReleaseVersion previous) =>
        previous.Major == 0
            ? Major > 0 || Minor > previous.Minor
            : Major > previous.Major;

    [GeneratedRegex(@"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<label>alpha|beta|rc)\.(?<number>[1-9]\d*))?$")]
    private static partial Regex SemVerRegex();

    [GeneratedRegex(@"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:(?<label>a|b|rc)(?<number>[1-9]\d*))?$")]
    private static partial Regex Pep440Regex();
}
