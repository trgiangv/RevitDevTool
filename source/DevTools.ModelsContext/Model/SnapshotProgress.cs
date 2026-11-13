namespace DevTools.ModelsContext.Model;

public readonly record struct SnapshotProgress(SourceId Source, int Collected, int Total);

public readonly record struct InstanceName(long Id, string Name);
