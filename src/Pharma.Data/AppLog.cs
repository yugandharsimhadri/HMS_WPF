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
    public static string LogDirectory
    {
        get
        {
            // Resolved once, but re-resolved if the override changes. Production
            // never sets it; tests do, and a value cached before they set it
            // would silently send their writes somewhere they cannot read.
            var over = Environment.GetEnvironmentVariable(DirectoryOverrideVariable);

            if (_resolved is not null && over == _resolvedFrom) return _resolved;

            _resolvedFrom = over;
            return _resolved = Resolve();
        }
    }

    private static string? _resolvedFrom;

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
    public static void Trace(string message) => Write("TRC", message, null);

    // ── Method tracing ─────────────────────────────────────────────────────

    /// <summary>Nesting depth per thread, so a log reads like a call stack.</summary>
    [ThreadStatic] private static int _depth;

    private static int _callSeq;

    /// <summary>
    /// Records entry to a method and, when the scope is disposed, how it left.
    ///
    /// The detail is the point: "SaveSaleAsync" tells you nothing an hour later,
    /// "SaveSaleAsync lines=2 customer=Aarav" tells you which bill. Pass the
    /// identifiers you would need to find the record again.
    ///
    /// <code>
    /// using var log = AppLog.Enter(nameof(SaveSaleAsync), $"lines={lines.Count}");
    /// ...
    /// log.Ok($"bill={sale.BillNo}");
    /// </code>
    ///
    /// A scope disposed without <see cref="MethodScope.Ok"/> is logged as having
    /// left without completing, which is how a thrown exception shows up — the
    /// exception itself is logged where it is caught, and this marks the exact
    /// method it escaped.
    /// </summary>
    public static MethodScope Enter(string method, string? detail = null)
    {
        if (!AppConfig.Current.TraceMethods) return MethodScope.Disabled;

        var id = Interlocked.Increment(ref _callSeq);

        Trace($"{Pad(_depth)}→ {method}#{id}{Detail(detail)}");
        _depth++;

        return new MethodScope(method, id);
    }

    private static string Detail(string? detail)
        => string.IsNullOrWhiteSpace(detail) ? "" : $" [{detail}]";

    private static string Pad(int depth) => depth <= 0 ? "" : new string(' ', Math.Min(depth, 12) * 2);

    /// <summary>Closes the entry written by <see cref="Enter"/>. Never throws.</summary>
    public readonly struct MethodScope : IDisposable
    {
        private readonly string? _method;
        private readonly int _id;
        private readonly long _startedTicks;
        private readonly Outcome? _outcome;

        internal static MethodScope Disabled => default;

        internal MethodScope(string method, int id)
        {
            _method = method;
            _id = id;
            _startedTicks = DateTime.UtcNow.Ticks;
            _outcome = new Outcome();
        }

        /// <summary>Marks the method as having finished, with whatever identifies the result.</summary>
        public void Ok(string? detail = null)
        {
            if (_outcome is null) return;

            _outcome.Completed = true;
            _outcome.Detail = detail;
        }

        /// <summary>Records a decision to stop early — a validation refusal, nothing to do.</summary>
        public void Skip(string reason)
        {
            if (_outcome is null) return;

            _outcome.Completed = true;
            _outcome.Skipped = true;
            _outcome.Detail = reason;
        }

        public void Dispose()
        {
            if (_method is null || _outcome is null) return;

            try
            {
                _depth = Math.Max(0, _depth - 1);

                var ms = (DateTime.UtcNow.Ticks - _startedTicks) / TimeSpan.TicksPerMillisecond;

                if (!_outcome.Completed)
                {
                    // Nothing marked it done, so it left the hard way.
                    Write("WRN", $"{Pad(_depth)}✗ {_method}#{_id} left without completing after {ms}ms", null);
                    return;
                }

                var arrow = _outcome.Skipped ? "↩" : "←";
                Trace($"{Pad(_depth)}{arrow} {_method}#{_id} {ms}ms{Detail(_outcome.Detail)}");
            }
            catch (Exception)
            {
                // Tracing must never be the reason something fails.
            }
        }

        /// <summary>Mutable state a readonly struct can still reach through.</summary>
        private sealed class Outcome
        {
            public bool Completed;
            public bool Skipped;
            public string? Detail;
        }
    }

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
