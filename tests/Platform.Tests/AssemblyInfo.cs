// Low-latency daemons saturate hardware cores and track process-wide allocations.
// Prevent xUnit from running test classes concurrently in the same process.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
