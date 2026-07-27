using Xunit;

// Logging is process-global: AppLog resolves one directory for the whole
// process, and every service call writes to it. Two classes running at once
// therefore write into the same file, and the tests that read the log back see
// each other's lines — which is exactly how the trace tests started failing
// once the reports tests arrived beside them.
//
// The suite runs in seconds, so serialising it costs little and removes a whole
// class of confusing, order-dependent failures.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
