using Xunit;

// DatabaseInitializationHelper is static and is touched by many tests (some call
// MarkAsInitialized in their constructor, the new timeout tests reset it). Running
// those in parallel makes the shared state races unreliable, so disable assembly-wide
// parallelization. The suite is small enough that the wall-clock cost is negligible.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
