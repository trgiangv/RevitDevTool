using Xunit;

// NUnitRunLoggingScope redirects process-wide Console/Trace; parallel runs race on SetOut.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
