using Microsoft.EntityFrameworkCore;

namespace Pharma.Data;

/// <summary>
/// Hands out gap-free sequential document numbers. Tax invoice numbering must be
/// sequential, so the counter is incremented inside the same transaction as the
/// document that consumes it.
/// </summary>
public static class NumberService
{
    public const string Patient = "Patient";
    public const string Visit = "Visit";
    public const string Bill = "Bill";
    public const string StockEntry = "StockEntry";
    public const string FeeReceipt = "FeeReceipt";
    public const string DiagnosticBill = "DiagnosticBill";

    public static async Task<string> NextAsync(AppDbContext db, string name, CancellationToken ct = default)
    {
        var counter = await db.Counters.FirstOrDefaultAsync(c => c.Name == name, ct);
        if (counter is null)
        {
            counter = new Core.Counter { Name = name, Prefix = DefaultPrefix(name), LastNumber = 0 };
            db.Counters.Add(counter);
        }

        counter.LastNumber++;
        return $"{counter.Prefix}{counter.LastNumber:D5}";
    }

    private static string DefaultPrefix(string name) => name switch
    {
        Patient => "P",
        Visit => "V",
        Bill => "INV",
        StockEntry => "GRN",
        FeeReceipt => "RCP",
        DiagnosticBill => "DX",
        _ => "DOC"
    };
}
