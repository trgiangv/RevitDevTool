using Xunit.Sdk;
using Xunit.v3;

// Runtime TestingRunTraceScope mutates process-wide Trace.Listeners during host spike runs.
[assembly: Parallelization(Mode = ParallelMode.None)]
