using MissingReferenceDependency;

namespace MissingReferenceConsumer;

public sealed class BrokenCommand : MissingBase, IMissingContract;

public sealed class WorkingCommand : IDisposable
{
    public MissingValue? Value { get; set; }

    [MissingMarker]
    [Obsolete("marker")]
    public void Run()
    {
    }

    public void Dispose()
    {
    }
}
