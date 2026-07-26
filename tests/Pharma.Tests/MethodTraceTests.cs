using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// The trace has one job: let someone reading a log a week later work out which
/// record a problem happened to. So it has to carry identifiers, has to close
/// every line it opens, and must never be the thing that breaks.
/// </summary>
[Collection("Logging")]
public class MethodTraceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"twinkle-trace-{Guid.NewGuid():N}");

    public MethodTraceTests()
    {
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable(AppLog.DirectoryOverrideVariable, _dir);
    }

    private string LogText() => File.Exists(AppLog.CurrentFile) ? File.ReadAllText(AppLog.CurrentFile) : "";

    [Fact]
    public void Entry_and_exit_are_both_written_with_the_detail()
    {
        using (var log = AppLog.Enter("ProbeSaveSale", "customer='Aarav' lines=2"))
        {
            log.Ok("bill=INV-0007 net=112.00");
        }

        var text = LogText();

        // Entry carries what went in, exit carries what came out.
        Assert.Contains("→ ProbeSaveSale", text);
        Assert.Contains("customer='Aarav' lines=2", text);
        Assert.Contains("← ProbeSaveSale", text);
        Assert.Contains("bill=INV-0007 net=112.00", text);
    }

    [Fact]
    public void The_same_call_can_be_matched_from_entry_to_exit()
    {
        int Id(string line) => int.Parse(line.Split('#')[1].Split(' ')[0]);

        using (var log = AppLog.Enter("ProbeAllocate", "product=abc wanted=9")) log.Ok("shortfall=0");

        var lines = LogText().Split('\n');

        var entry = lines.Single(l => l.Contains("→ ProbeAllocate"));
        var exit = lines.Single(l => l.Contains("← ProbeAllocate"));

        // Nested and interleaved calls are common; the number is what pairs them.
        Assert.Equal(Id(entry), Id(exit));
    }

    [Fact]
    public void A_method_that_throws_is_marked_as_not_having_completed()
    {
        try
        {
            using var log = AppLog.Enter("ProbeQuickAdd", "product=abc packs=0");
            throw new InvalidOperationException("Enter how many packs are on the shelf.");
        }
        catch (InvalidOperationException)
        {
            // The caller handles it; the trace still has to show where it left.
        }

        var text = LogText();

        Assert.Contains("→ ProbeQuickAdd", text);
        Assert.Contains("✗ ProbeQuickAdd", text);
        Assert.Contains("left without completing", text);
    }

    [Fact]
    public void A_refusal_reads_differently_from_a_failure()
    {
        using (var log = AppLog.Enter("ProbeAddLine", "qty=7")) log.Skip("loose sale refused");

        var text = LogText();

        Assert.Contains("↩ ProbeAddLine", text);
        Assert.Contains("loose sale refused", text);
        Assert.DoesNotContain("left without completing", text);
    }

    [Fact]
    public void Nested_calls_are_indented_so_the_log_reads_as_a_call_stack()
    {
        using (var bill = AppLog.Enter("ProbeSaveBill"))
        {
            using (var allocate = AppLog.Enter("ProbeInner")) allocate.Ok();
            bill.Ok();
        }

        var lines = LogText().Split('\n');

        var inner = lines.Single(l => l.Contains("→ ProbeInner"));
        var outer = lines.Single(l => l.Contains("→ ProbeSaveBill"));

        Assert.True(Indent(inner) > Indent(outer), "the inner call should be indented further");

        static int Indent(string line)
        {
            var body = line[(line.IndexOf("TRC", StringComparison.Ordinal) + 3)..];
            return body.Length - body.TrimStart().Length;
        }
    }

    [Fact]
    public void Depth_does_not_drift_when_scopes_are_disposed()
    {
        for (var i = 0; i < 5; i++)
        {
            using var log = AppLog.Enter("Repeated");
            log.Ok();
        }

        // Every one of them starts at the same indent, so a long session does not
        // slowly march off the right-hand side of the file.
        var entries = LogText().Split('\n').Where(l => l.Contains("→ Repeated")).ToList();

        Assert.Equal(5, entries.Count);
        Assert.Single(entries.Select(l => l.IndexOf('→')).Distinct());
    }

    [Fact]
    public void Tracing_never_throws()
    {
        // Null detail, disposing twice, and using it after completion.
        var log = AppLog.Enter("Odd", null);
        log.Ok(null);
        log.Dispose();
        log.Dispose();

        Assert.Contains("→ Odd", LogText());
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(AppLog.DirectoryOverrideVariable, null);
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }
}
