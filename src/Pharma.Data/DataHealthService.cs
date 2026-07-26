using Microsoft.EntityFrameworkCore;
using Pharma.Core;

namespace Pharma.Data;

/// <summary>What is wrong with one medicine, and what putting it right would do.</summary>
public class HealthFinding
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = "";
    public HealthProblem Problem { get; init; }

    /// <summary>What the record says now.</summary>
    public string Current { get; init; } = "";

    /// <summary>What it would say after the repair.</summary>
    public string Proposed { get; init; } = "";

    /// <summary>Plain words for someone who did not write the software.</summary>
    public string Explanation { get; init; } = "";

    // Only set for the pack-size problem, where stock is re-counted.
    public int UnitsPerPack { get; init; }
    public int QuantityBefore { get; init; }
    public int QuantityAfter { get; init; }
    public DispensingUnit InferredUnit { get; init; }

    public bool ChangesStock => QuantityAfter != QuantityBefore;

    /// <summary>Whether repairing this can be done without a human deciding anything.</summary>
    public bool CanRepairAutomatically => Problem != HealthProblem.Duplicate;
}

public enum HealthProblem
{
    /// <summary>Pack size states a count that units-per-pack disagrees with.</summary>
    PackSizeDisagrees = 1,

    /// <summary>Batches were received under a different pack size than the medicine now says.</summary>
    BatchPackDisagrees,

    /// <summary>Dispensing unit was never set, so quantities read as "units".</summary>
    UnitNotSet,

    /// <summary>Two medicines that are the same thing. Needs a human to choose.</summary>
    Duplicate
}

/// <summary>
/// Finds medicines whose records cannot be right, and repairs them together.
///
/// This exists because the damage is silent. A medicine whose pack size says
/// "15 TAB" while units-per-pack says 1 sells whole strips to anyone asking for
/// tablets, at fifteen times the price, and nothing anywhere reports an error.
/// Fixing them one at a time is not a fix when a shop has two hundred.
/// </summary>
public class DataHealthService(IDbContextFactory<AppDbContext> factory, PharmacyService pharmacy)
{
    public async Task<List<HealthFinding>> ScanAsync()
    {
        using var log = AppLog.Enter(nameof(ScanAsync));

        await using var db = await factory.CreateDbContextAsync();

        var products = await db.Products
            .Include(p => p.Batches)
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync();

        var findings = new List<HealthFinding>();

        foreach (var product in products)
        {
            AddPackFinding(findings, product);
            AddUnitFinding(findings, product);
        }

        AddDuplicateFindings(findings, products);

        log.Ok($"{findings.Count} finding(s) across {products.Count} medicine(s)");
        return findings;
    }

    /// <summary>
    /// "15 TAB" and one-unit-per-pack cannot both be true. The pack size is what
    /// is printed on the box, so it wins.
    /// </summary>
    private static void AddPackFinding(List<HealthFinding> findings, Product product)
    {
        var stated = PackMath.UnitsFromPacking(product.PackSize);
        var live = product.Batches.Where(b => !b.IsDeleted).ToList();

        if (stated is { } n && n != product.UnitsPerPack)
        {
            var before = live.Sum(b => b.QtyOnHand);
            var after = live.Sum(b => b.QtyOnHand / Math.Max(1, b.UnitsPerPack) * n);

            findings.Add(new HealthFinding
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Problem = HealthProblem.PackSizeDisagrees,
                Current = $"pack says {n}, medicine says {product.UnitsPerPack} per pack",
                Proposed = $"{n} per pack",
                UnitsPerPack = n,
                QuantityBefore = before,
                QuantityAfter = after,
                Explanation = before == after
                    ? $"The counter is selling whole packs to anyone asking for " +
                      $"{product.DispensingUnit.Name(2)}."
                    : $"The counter is selling whole packs at {n} times the price. " +
                      $"Stock is re-counted from {before} to {after} — the same packs on the shelf."
            });

            return;
        }

        // The medicine may be right while stock received earlier is not.
        var stale = live.Where(b => b.UnitsPerPack != product.UnitsPerPack).ToList();
        if (stale.Count == 0) return;

        var staleBefore = stale.Sum(b => b.QtyOnHand);
        var staleAfter = stale.Sum(b => b.QtyOnHand / Math.Max(1, b.UnitsPerPack) * product.UnitsPerPack);

        findings.Add(new HealthFinding
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Problem = HealthProblem.BatchPackDisagrees,
            Current = $"{stale.Count} batch(es) at a different pack size",
            Proposed = $"{product.UnitsPerPack} per pack",
            UnitsPerPack = product.UnitsPerPack,
            QuantityBefore = staleBefore,
            QuantityAfter = staleAfter,
            Explanation = $"Stock received before the medicine was corrected is still " +
                          $"sold by the pack. Re-counting brings it in line."
        });
    }

    /// <summary>
    /// Rows written before the dispensing unit existed hold nothing, so every
    /// quantity on screen reads "units" instead of tablets or bottles.
    /// </summary>
    private static void AddUnitFinding(List<HealthFinding> findings, Product product)
    {
        if (Enum.IsDefined(product.DispensingUnit)) return;

        var inferred = InferUnit(product.PackSize);

        findings.Add(new HealthFinding
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Problem = HealthProblem.UnitNotSet,
            Current = "not set",
            Proposed = inferred.Name(1),
            InferredUnit = inferred,
            Explanation = $"Quantities read as \"units\" instead of {inferred.Name(2)}."
        });
    }

    /// <summary>Taken from what is printed on the pack: "10 TAB", "100 ML", "21.8 G".</summary>
    public static DispensingUnit InferUnit(string? packSize)
    {
        var text = (packSize ?? "").ToUpperInvariant();

        if (text.Contains("CAP")) return DispensingUnit.Capsule;
        if (text.Contains("TAB")) return DispensingUnit.Tablet;
        if (text.Contains("ML")) return DispensingUnit.Bottle;
        if (text.Contains("VIAL")) return DispensingUnit.Vial;
        if (text.Contains("GM") || text.Contains(" G")) return DispensingUnit.Sachet;

        return DispensingUnit.Tablet;
    }

    /// <summary>
    /// Same name, same maker, same pack is the same medicine twice. Reported but
    /// never repaired automatically — which one survives is a decision, and the
    /// wrong choice loses stock.
    /// </summary>
    private static void AddDuplicateFindings(List<HealthFinding> findings, List<Product> products)
    {
        var groups = products
            .GroupBy(Key)
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            foreach (var product in group.OrderByDescending(p => p.StockOnHand).Skip(1))
            {
                var keeper = group.OrderByDescending(p => p.StockOnHand).First();

                findings.Add(new HealthFinding
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Problem = HealthProblem.Duplicate,
                    Current = $"{group.Count()} records of this medicine",
                    Proposed = $"merge into the one holding {keeper.StockOnHand}",
                    Explanation = "It appears twice at the counter, and stock is split " +
                                  "between them. Merging has to be done by hand."
                });
            }
        }
    }

    /// <summary>Trimmed, case-folded, whitespace-collapsed — so near-misses collide.</summary>
    public static string Key(Product product)
    {
        static string Norm(string? value) =>
            string.Join(' ', (value ?? "").Trim().ToLowerInvariant()
                                          .Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return $"{Norm(product.Name)}|{Norm(product.Manufacturer)}|{Norm(product.PackSize)}";
    }

    /// <summary>
    /// Applies the chosen repairs. Each medicine is done on its own so one
    /// failure cannot take the rest with it, and anything that moves a stock
    /// figure writes its own audit row.
    /// </summary>
    public async Task<int> RepairAsync(IEnumerable<HealthFinding> findings, string? by = null)
    {
        using var log = AppLog.Enter(nameof(RepairAsync), $"by={by}");

        var repaired = 0;

        foreach (var finding in findings.Where(f => f.CanRepairAutomatically))
        {
            switch (finding.Problem)
            {
                case HealthProblem.PackSizeDisagrees:
                case HealthProblem.BatchPackDisagrees:
                    // Re-counts the batches and writes an adjustment for each.
                    await pharmacy.RepackAsync(finding.ProductId, finding.UnitsPerPack, by);
                    break;

                case HealthProblem.UnitNotSet:
                    await SetUnitAsync(finding.ProductId, finding.InferredUnit);
                    break;
            }

            repaired++;
        }

        log.Ok($"{repaired} repaired");
        return repaired;
    }

    private async Task SetUnitAsync(Guid productId, DispensingUnit unit)
    {
        await using var db = await factory.CreateDbContextAsync();

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product is null) return;

        product.DispensingUnit = unit;
        await db.SaveChangesAsync();

        AppLog.Info($"Dispensing unit set: {product.Name} → {unit}.");
    }
}
