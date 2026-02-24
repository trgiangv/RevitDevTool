namespace RevitDevTool.Console.RevitFileInfo.BasicFileStream;

public class ModelIdentity : IEquatable<ModelIdentity>
{
    private readonly Guid _guid;

    public static readonly ModelIdentity Empty = new(Guid.Empty);

    public ModelIdentity(Guid guid) => _guid = guid;

    public override string ToString() => _guid.ToString();

    public bool Equals(ModelIdentity? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return _guid.Equals(other._guid);
    }

    public override bool Equals(object? obj) => obj is ModelIdentity other && Equals(other);
    public override int GetHashCode() => _guid.GetHashCode();
}
