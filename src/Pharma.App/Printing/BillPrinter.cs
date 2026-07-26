using System.Windows;
using System.Windows.Documents;
using Pharma.Core;
using Pharma.Data;
using static Pharma.App.Printing.DocumentBuilder;

namespace Pharma.App.Printing;

/// <summary>
/// Retail tax invoice. Carries the details a chemist's bill must show in India:
/// GSTIN, drug licence number, and batch plus expiry against every line.
/// </summary>
public static class BillPrinter
{
    public static FlowDocument Build(Sale sale, ShopProfile shop, bool isReprint = false)
    {
        var doc = NewDocument();

        AddClinicHeader(doc, shop, isReprint ? "TAX INVOICE (DUPLICATE)" : "TAX INVOICE");

        var head = NewTable(1, 1);
        var headGroup = new TableRowGroup();
        headGroup.Rows.Add(Row(false,
            $"Bill No: {sale.BillNo}",
            $"Date: {sale.BillDate:dd MMM yyyy  HH:mm}"));
        headGroup.Rows.Add(Row(false,
            $"Patient: {sale.CustomerName}",
            string.IsNullOrWhiteSpace(sale.DoctorName) ? "" : $"Doctor: {sale.DoctorName}"));
        head.RowGroups.Add(headGroup);
        doc.Blocks.Add(head);

        var items = NewTable(3.2, 1.4, 0.9, 0.7, 0.9, 0.7, 1.1);
        var itemGroup = new TableRowGroup();
        itemGroup.Rows.Add(Row(true, "MEDICINE", "BATCH", "EXPIRY", "QTY", "MRP", "GST%", "AMOUNT"));

        foreach (var item in sale.Items)
        {
            itemGroup.Rows.Add(Row(false,
                item.ProductName,
                item.BatchNo,
                // Quoted separator: a bare "/" is replaced by the machine's date
                // separator, which made the printed expiry differ between PCs.
                item.ExpiryDate.ToString("MM'/'yy"),
                item.UnitsPerPack > 1 ? item.QuantityDescription : item.Quantity.ToString(),
                item.Mrp.ToString("0.00"),
                item.GstRate.ToString("0.#"),
                item.LineTotal.ToString("0.00")));
        }

        items.RowGroups.Add(itemGroup);
        doc.Blocks.Add(items);

        // GST summary grouped by rate — what makes this a valid tax invoice.
        var slabs = sale.Items.GroupBy(i => i.GstRate).OrderBy(g => g.Key).ToList();
        if (slabs.Count > 0)
        {
            doc.Blocks.Add(Text("GST SUMMARY", 9.5, FontWeights.SemiBold, Muted, topMargin: 4));

            var gst = NewTable(1.2, 1.4, 1.2, 1.2, 1.2);
            var gstGroup = new TableRowGroup();
            gstGroup.Rows.Add(Row(true, "RATE", "TAXABLE", "CGST", "SGST", "TOTAL GST"));

            foreach (var slab in slabs)
            {
                var taxable = slab.Sum(i => i.TaxableAmount);
                var tax = slab.Sum(i => i.GstAmount);
                var half = Math.Round(tax / 2m, 2, MidpointRounding.AwayFromZero);

                gstGroup.Rows.Add(Row(false,
                    $"{slab.Key:0.#}%",
                    taxable.ToString("0.00"),
                    (tax - half).ToString("0.00"),
                    half.ToString("0.00"),
                    tax.ToString("0.00")));
            }

            gst.RowGroups.Add(gstGroup);
            doc.Blocks.Add(gst);
        }

        doc.Blocks.Add(Rule());

        var totals = NewTable(3, 0.2, 1.2);
        var totalGroup = new TableRowGroup();
        totalGroup.Rows.Add(Row(false, "Gross", "", sale.GrossAmount.ToString("0.00")));
        if (sale.DiscountAmount > 0)
            totalGroup.Rows.Add(Row(false, "Discount", "", $"-{sale.DiscountAmount:0.00}"));
        totalGroup.Rows.Add(Row(false, "Taxable value", "", sale.TaxableAmount.ToString("0.00")));
        totalGroup.Rows.Add(Row(false, "CGST", "", sale.CgstAmount.ToString("0.00")));
        totalGroup.Rows.Add(Row(false, "SGST", "", sale.SgstAmount.ToString("0.00")));
        if (sale.RoundOff != 0)
            totalGroup.Rows.Add(Row(false, "Round off", "", sale.RoundOff.ToString("+0.00;-0.00")));
        totals.RowGroups.Add(totalGroup);
        doc.Blocks.Add(totals);

        doc.Blocks.Add(Text($"NET PAYABLE   ₹{sale.NetAmount:0.00}", 16, FontWeights.Bold,
                            align: TextAlignment.Right, topMargin: 2));
        doc.Blocks.Add(Text($"Paid by {sale.PaymentMode}", 10, brush: Muted, align: TextAlignment.Right));
        doc.Blocks.Add(Text(FeeReceiptDocument.InWords(sale.NetAmount), 10, brush: Muted, topMargin: 4));

        doc.Blocks.Add(Rule());

        if (!string.IsNullOrWhiteSpace(shop.PharmacistName))
            doc.Blocks.Add(Text($"Pharmacist: {shop.PharmacistName}", 10, brush: Muted));

        if (sale.Items.Count > 0)
            doc.Blocks.Add(Text($"HSN: {string.Join(", ", sale.Items.Select(i => i.HsnCode).Distinct())}",
                                9.5, brush: Muted));

        if (!string.IsNullOrWhiteSpace(shop.BillFooter))
            doc.Blocks.Add(Text(shop.BillFooter, 9.5, brush: Muted, align: TextAlignment.Center, topMargin: 8));

        return doc;
    }
}
