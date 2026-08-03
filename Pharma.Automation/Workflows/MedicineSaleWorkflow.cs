using Pharma.Automation.Support;

namespace Pharma.Automation;

/// <summary>
/// Stocks a fresh medicine, then sells some of it at the pharmacy counter and
/// saves the bill.
/// </summary>
public class MedicineSaleWorkflow : IWorkflow
{
    public string Name => "MedicineSale";

    public void Execute(AppFixture app)
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"Demo Sale Drug {suffix}";

        ProductStockSupport.CreateProductWithStock(app, name, $"B{suffix}", packs: 40, mrp: 60m);

        app.Navigate("NavSale", "Pharmacy counter");
        app.Type("SaleSearch", name);
        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length == 1, "the medicine to be found");
        app.ListBox("SaleMatches").Items[0].Select();

        app.Type("SaleCustomerName", "Demo Customer");
        app.Type("SaleQuantity", "2");
        app.Click("SaleAddLine");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the line to be added");

        app.Click("SaleSave");
        AppFixture.WaitUntil(
            () => app.TextOf("SaleStatus").Contains("INV", StringComparison.Ordinal),
            "the bill to be numbered");
    }
}
