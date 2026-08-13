using Xunit;

// Runtime NUnitRunTraceScope mutates process-wide Trace.Listeners during host spike runs.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
