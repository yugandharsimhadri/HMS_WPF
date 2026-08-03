using Pharma.Automation.Support;

namespace Pharma.Automation;

/// <summary>
/// A goods delivery: create the medicine, then receive an opening batch of
/// stock for it — the "purchase" side of the pharmacy, as distinct from
/// Inventory's stock-count correction.
/// </summary>
public class PurchaseWorkflow : IWorkflow
{
    public string Name => "Purchase";

    public void Execute(AppFixture app)
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"Demo Purchase Drug {suffix}";

        ProductStockSupport.CreateProductWithStock(app, name, $"P{suffix}", packs: 50, mrp: 112m);
    }
}
