using Xunit;

// Runtime TestingRunTraceScope mutates process-wide Trace.Listeners during host spike runs.
#pragma warning disable CS0619 // CollectionBehavior.DisableTestParallelization — xUnit 4 Parallelization attribute not on compile surface yet
[assembly: CollectionBehavior(DisableTestParallelization = true)]
#pragma warning restore CS0619
