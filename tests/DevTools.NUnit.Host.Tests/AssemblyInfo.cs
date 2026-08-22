using Xunit;

// Runtime TestingRunTraceScope mutates process-wide Trace.Listeners during host spike runs.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
