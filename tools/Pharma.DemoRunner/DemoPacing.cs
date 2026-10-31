using System.Globalization;

namespace Pharma.DemoRunner;

/// <summary>
/// How long a workflow occupies the screen — which, for a recording, is a
/// different question from how long it takes to run.
/// </summary>
/// <remarks>
/// A workflow that finishes in four seconds is correct, fast, and impossible
/// to narrate over: a voiceover explaining a module needs ten to twenty
/// seconds, and six is one short sentence. The fix is deliberately not to slow
/// the clicks down — a demo of a sluggish-looking product sells nothing.
/// Instead the runner holds the workflow's own final screen until the minimum
/// is reached. The narration gets a stable frame to talk over and the product
/// still looks as quick as it is.
///
/// Off unless asked for. A bare run is the fast one, unchanged.
/// </remarks>
internal sealed class DemoPacing
{
    /// <summary>Set HMS_DEMO_RUN_MODE=demo to pace a run for recording.</summary>
    public const string ModeVariable = "HMS_DEMO_RUN_MODE";

    /// <summary>
    /// Set HMS_DEMO_MIN_SECONDS_PER_WORKFLOW to change the hold. The content
    /// engine that records this passes the value from its own product
    /// configuration, so the pacing is tunable from there without a code
    /// change in either repository.
    /// </summary>
    public const string MinimumSecondsVariable = "HMS_DEMO_MIN_SECONDS_PER_WORKFLOW";

    /// <summary>The only value of <see cref="ModeVariable"/> that paces anything.</summary>
    public const string PacedMode = "demo";

    private static readonly TimeSpan DefaultMinimum = TimeSpan.FromSeconds(12);

    private DemoPacing(bool isPaced, TimeSpan minimum)
    {
        IsPaced = isPaced;
        Minimum = minimum;
    }

    public bool IsPaced { get; }

    /// <summary>How long each workflow is given on screen; zero when not pacing.</summary>
    public TimeSpan Minimum { get; }

    /// <summary>Time spent waiting rather than working, across the whole run.</summary>
    public TimeSpan TotalHeld { get; private set; }

    public static DemoPacing FromEnvironment()
    {
        var mode = (Environment.GetEnvironmentVariable(ModeVariable) ?? "").Trim();
        var isPaced = string.Equals(mode, PacedMode, StringComparison.OrdinalIgnoreCase);

        // A value nobody recognises would otherwise run at test speed in
        // silence, which looks exactly like the pacing being broken.
        if (!isPaced && mode.Length > 0)
            Console.WriteLine($"{ModeVariable}='{mode}' is not '{PacedMode}' — running at test speed.");

        return new DemoPacing(isPaced, isPaced ? ReadMinimum() : TimeSpan.Zero);
    }

    private static TimeSpan ReadMinimum()
    {
        var raw = (Environment.GetEnvironmentVariable(MinimumSecondsVariable) ?? "").Trim();
        if (raw.Length == 0) return DefaultMinimum;

        // Invariant parsing on purpose: this arrives from another process's
        // configuration, where "12.5" must not come out as 125 because of the
        // recording machine's decimal separator.
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            Console.WriteLine(
                $"{MinimumSecondsVariable}='{raw}' is not a number — using {Seconds(DefaultMinimum)}.");
            return DefaultMinimum;
        }

        // Zero is a legitimate answer ("hold nothing"). Negative is not.
        return seconds > 0 ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero;
    }

    /// <summary>The one line that explains the rest of the run's timing.</summary>
    public void Announce()
    {
        if (!IsPaced) return;

        Console.WriteLine(
            $"Demo pacing on: every workflow holds its final screen for at least {Seconds(Minimum)} " +
            $"(set {MinimumSecondsVariable} to change it).");
    }

    /// <summary>
    /// Keeps the workflow's last screen up until it has had its minimum.
    /// Workflows that already ran longer than that are never cut short.
    /// </summary>
    public void HoldFinalScreen(string workflowName, TimeSpan elapsed)
    {
        if (!IsPaced) return;

        var remaining = Minimum - elapsed;
        if (remaining <= TimeSpan.Zero) return;

        Console.WriteLine(
            $"Holding {workflowName} for {Seconds(remaining)} — it ran in {Seconds(elapsed)}, " +
            $"short of the {Seconds(Minimum)} minimum.");

        Thread.Sleep(remaining);
        TotalHeld += remaining;
    }

    /// <summary>What the pacing cost the run, once, at the end.</summary>
    public void Summarise()
    {
        if (!IsPaced || TotalHeld <= TimeSpan.Zero) return;

        Console.WriteLine($"Pacing held {Seconds(TotalHeld)} in total.");
    }

    private static string Seconds(TimeSpan span)
        => $"{span.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)}s";
}
