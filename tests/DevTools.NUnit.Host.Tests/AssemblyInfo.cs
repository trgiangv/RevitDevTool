using Xunit.Sdk;
using Xunit.v3;

// Runtime TestRunTraceScope mutates process-wide Trace.Listeners during host spike runs.
[assembly: Parallelization(Mode = ParallelMode.None)]
