using System.Text;

namespace Pharma.Data;

/// <summary>
/// Plain file logging, one file per day next to the database. Deliberately tiny
/// and dependency-free: a clinic PC has no log server, and the first thing anyone
/// needs after a crash is a file they can attach to an email.
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private static bool _pruned;
    private static string? _resolved;

    /// <summary>Overrides the configured folder. Used by tests.</summary>
    public const string DirectoryOverrideVariable = "TWINKLE_LOG_DIR";

    /// <summary>The folder that was asked for but could not be used, if any.</summary>
    public static string? FallbackReason { get; private set; }

    /// <summary>
    /// Resolved once, in order of preference, and proven writable before it is
    /// used. A log folder that cannot be written to is worse than useless — the
    /// first thing anyone asks for after a problem is the log — so a folder the
    /// clinic PC will not allow silently steps down to one it will.
    /// </summary>
    public static string LogDirectory => _resolved ??= Resolve();

    private static string Resolve()
    {
        var configured = Environment.GetEnvironmentVariable(DirectoryOverrideVariable);
        if (string.IsNullOrWhiteSpace(configured)) configured = AppConfig.Current.LogDirectory;

        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured);

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TwinkleHMS", "logs"));

        candidates.Add(Path.Combine(Path.GetTempPath(), "TwinkleHMS", "logs"));

        foreach (var candidate in candidates)
        {
            if (!IsUsable(candidate)) continue;

            if (candidate != candidates[0])
            {
                FallbackReason =
                    $"'{candidates[0]}' could not be written to, so logs are going to '{candidate}' instead.";
            }

            return candidate;
        }

        // Nothing was writable. Writing then fails quietly rather than throwing.
        return candidates[0];
    }

    private static bool IsUsable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);

            var probe = Path.Combine(directory, $".probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string CurrentFile => Path.Combine(LogDirectory, $"twinkle-{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string message) => Write("INF", message, null);
    public static void Warn(string message) => Write("WRN", message, null);
    public static void Error(string message, Exception? ex = null) => Write("ERR", message, ex);

    /// <summary>
    /// Logs anything thrown by a task nobody is awaiting. Every fire-and-forget
    /// call in the UI goes through this so a failure lands in the log instead of
    /// disappearing or tearing the process down.
    /// </summary>
    public static void Forget(this Task task, string context)
        => task.ContinueWith(
            t => Error($"{context} failed", t.Exception?.GetBaseException()),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            var line = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(' ').Append(level)
                .Append(' ').Append(message);

            if (ex is not null)
            {
                line.AppendLine()
                    .Append("    ").Append(ex.GetType().Name).Append(": ").Append(ex.Message)
                    .AppendLine()
                    .Append(Indent(ex.StackTrace));

                if (ex.InnerException is { } inner)
                {
                    line.AppendLine()
                        .Append("    caused by ").Append(inner.GetType().Name).Append(": ").Append(inner.Message);
                }
            }

            lock (Gate)
            {
                PruneOnce();
                File.AppendAllText(CurrentFile, line.AppendLine().ToString());
            }
        }
        catch (Exception)
        {
            // Logging must never be the reason the application stops working.
        }
    }

    private static string Indent(string? stackTrace)
        => string.IsNullOrWhiteSpace(stackTrace)
            ? "    (no stack trace)"
            : string.Join(Environment.NewLine,
                stackTrace.Split('\n').Select(l => "    " + l.TrimEnd('\r')));

    /// <summary>Keeps a month of logs; a busy counter still only writes a few KB a day.</summary>
    private static void PruneOnce()
    {
        if (_pruned) return;
        _pruned = true;

        try
        {
            foreach (var stale in new DirectoryInfo(LogDirectory)
                         .GetFiles("twinkle-*.log")
                         .OrderByDescending(f => f.Name)
                         .Skip(Math.Max(1, AppConfig.Current.LogDaysToKeep)))
            {
                stale.Delete();
            }
        }
        catch (IOException) { }
    }
}
