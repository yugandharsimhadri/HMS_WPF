// UI Automation drives a single desktop — nothing here may run concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
