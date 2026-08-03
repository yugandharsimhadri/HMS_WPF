using Pharma.Automation.Support;

namespace Pharma.Automation;

/// <summary>
/// Stocks a fresh medicine, then corrects its count with a reason — the
/// stock-audit side of the pharmacy, as distinct from Purchase's goods receipt.
/// </summary>
public class InventoryWorkflow : IWorkflow
{
    public string Name => "Inventory";

    public void Execute(AppFixture app)
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"Demo Inventory Drug {suffix}";

        ProductStockSupport.CreateProductWithStock(app, name, $"C{suffix}", packs: 4, mrp: 25.00m);

        app.Navigate("NavInventory", "Inventory");
        app.Type("InventorySearch", name);
        app.Click("InventorySearchButton");
        AppFixture.WaitUntil(() => app.Grid("InventoryProductsGrid").RowCount == 1, "the medicine in inventory");
        app.Grid("InventoryProductsGrid").Rows[0].Select();

        app.Click("InventoryCorrect");
        AppFixture.WaitUntil(() => app.Find("CorrectQuantity") is not null, "the correction form");

        // The box opens pre-filled with what the system believes is on the shelf;
        // read it back rather than assume a pack size, and correct it down by one.
        var onHand = int.Parse(app.TextBox("CorrectQuantity").Text);
        app.Type("CorrectQuantity", Math.Max(onHand - 1, 0).ToString());
        app.Type("CorrectNotes", $"Demo shelf count {suffix}");
        app.Click("CorrectStock");

        AppFixture.WaitUntil(() => app.Find("CorrectQuantity") is null, "the correction form to close");
    }
}
