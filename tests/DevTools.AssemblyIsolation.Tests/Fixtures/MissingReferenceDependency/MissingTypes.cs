namespace MissingReferenceDependency;

public interface IMissingContract;

public class MissingBase;

public sealed class MissingValue;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MissingMarkerAttribute : Attribute;
