using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace DevTools.Testing.Transport;

/// <summary>
/// A framework package version: <c>major[.minor[.build[.revision]]][-label][+build]</c>.
/// The numeric part is padded to four components, a prerelease sorts below its release,
/// and build metadata is ignored.
/// </summary>
public sealed record FrameworkVersion : IComparable<FrameworkVersion>
{
    private FrameworkVersion(Version numbers, string? label)
    {
        Numbers = numbers;
        Label = label;
    }

    public Version Numbers { get; }

    public string? Label { get; }

    public int Major => Numbers.Major;

    public static FrameworkVersion From(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return new FrameworkVersion(Pad(version.Major, version.Minor, version.Build, version.Revision), null);
    }

    public static FrameworkVersion Parse(string text) =>
        TryParse(text, out var version)
            ? version
            : throw new FormatException($"'{text}' is not a framework version.");

    public static bool TryParse(string? text, [NotNullWhen(true)] out FrameworkVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var value = text!.Trim();
        var plus = value.IndexOf('+');
        if (plus >= 0)
            value = value.Substring(0, plus);

        string? label = null;
        var dash = value.IndexOf('-');
        if (dash >= 0)
        {
            label = value.Substring(dash + 1);
            value = value.Substring(0, dash);
            if (label.Length == 0)
                return false;
        }

        var parts = value.Split('.');
        if (parts.Length > 4)
            return false;

        var numbers = new[] { 0, 0, 0, 0 };
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                return false;
        }

        version = new FrameworkVersion(Pad(numbers[0], numbers[1], numbers[2], numbers[3]), label);
        return true;
    }

    public int CompareTo(FrameworkVersion? other)
    {
        if (other is null)
            return 1;

        var numbers = Numbers.CompareTo(other.Numbers);
        if (numbers != 0)
            return numbers;

        return (Label, other.Label) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            _ => string.CompareOrdinal(Label, other.Label),
        };
    }

    public override string ToString()
    {
        var numbers = Numbers.Revision == 0 ? Numbers.ToString(3) : Numbers.ToString();
        return Label is null ? numbers : $"{numbers}-{Label}";
    }

    public static bool operator <(FrameworkVersion left, FrameworkVersion right) => Compare(left, right) < 0;

    public static bool operator >(FrameworkVersion left, FrameworkVersion right) => Compare(left, right) > 0;

    public static bool operator <=(FrameworkVersion left, FrameworkVersion right) => Compare(left, right) <= 0;

    public static bool operator >=(FrameworkVersion left, FrameworkVersion right) => Compare(left, right) >= 0;

    private static int Compare(FrameworkVersion? left, FrameworkVersion? right) =>
        left is null ? (right is null ? 0 : -1) : left.CompareTo(right);

    private static Version Pad(int major, int minor, int build, int revision) =>
        new(major, Math.Max(minor, 0), Math.Max(build, 0), Math.Max(revision, 0));
}
