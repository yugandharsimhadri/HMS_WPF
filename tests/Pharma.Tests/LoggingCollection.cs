namespace Pharma.Tests;

/// <summary>
/// The log directory is process-wide state, so anything that reads the log back
/// has to run on its own — otherwise two classes write into one file and each
/// sees the other's lines.
/// </summary>
[CollectionDefinition("Logging", DisableParallelization = true)]
public class LoggingCollection;
