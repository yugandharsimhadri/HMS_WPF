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
    public static FlowDocument Build(Sale sale, PharmacyProfile pharmacy, DocumentTheme theme, bool isReprint = false)
    {
        var doc = NewDocument();

        // A pharmacy that is not registered for GST issues a plain invoice. Calling
        // it a tax invoice, or printing a GSTIN on it, would be a false claim.
        var kind = sale.IsTaxInvoice ? "TAX INVOICE" : "INVOICE";

        AddPharmacyHeader(doc, pharmacy, theme, isReprint ? $"{kind} (DUPLICATE)" : kind, showGstin: sale.IsTaxInvoice);
        doc.Blocks.Add(Rule());

        // Date and time sit side by side rather than one under the other —
        // and the drug licence number gets its own labelled cell here,
        // exactly like Patient, rather than being buried in the header text.
        var head = NewTable(1, 1, 1);
        var headGroup = new TableRowGroup();
        headGroup.Rows.Add(IdentityRow(
            ("Bill No", sale.BillNo),
            ("Date", $"{sale.BillDate:dd/MM/yyyy}"),
            ("Time", $"{sale.BillDate:HH:mm}")));
        headGroup.Rows.Add(IdentityRow(
            ("Patient", sale.CustomerName),
            ("Doctor", sale.DoctorName ?? ""),
            ("D.L. No", pharmacy.DrugLicenceNo ?? "")));
        head.RowGroups.Add(headGroup);
        doc.Blocks.Add(head);
        doc.Blocks.Add(Rule());

        // The GST% column only earns its space on a tax invoice.
        var items = sale.IsTaxInvoice
            ? NewTable(3.2, 1.4, 0.9, 0.7, 0.9, 0.7, 1.1)
            : NewTable(3.6, 1.4, 0.9, 0.8, 1.0, 1.2);

        var itemGroup = new TableRowGroup();

        itemGroup.Rows.Add(sale.IsTaxInvoice
            ? Row(true, "MEDICINE", "BATCH", "EXPIRY", "QTY", "MRP", "GST%", "AMOUNT")
            : Row(true, "MEDICINE", "BATCH", "EXPIRY", "QTY", "MRP", "AMOUNT"));

        // One heading per medicine, its batches beneath it. A course that spans
        // two batches is two lines at two prices, and printed flat it reads like
        // a billing mistake to the parent holding it.
        foreach (var medicine in sale.Items.GroupBy(i => i.ProductName))
        {
            var split = medicine.Count() > 1;

            if (split)
            {
                var total = medicine.Sum(i => i.LineTotal).ToString("0.00");
                var quantity = medicine.Sum(i => i.Quantity);
                var unit = medicine.First();

                itemGroup.Rows.Add(sale.IsTaxInvoice
                    ? Row(false, medicine.Key, "", "",
                          PackMath.Describe(quantity, unit.UnitsPerPack, unit.PackLabel), "", "", total)
                    : Row(false, medicine.Key, "", "",
                          PackMath.Describe(quantity, unit.UnitsPerPack, unit.PackLabel), "", total));
            }

            foreach (var item in medicine)
            {
                // Quoted separator: a bare "/" is replaced by the machine's date
                // separator, which made the printed expiry differ between PCs.
                var expiry = item.ExpiryDate.ToString("MM'/'yy");
                var qty = item.UnitsPerPack > 1 ? item.QuantityDescription : item.Quantity.ToString();

                // Indented under the heading when this medicine came from more
                // than one batch, so the batches read as detail, not as extra items.
                var name = split ? $"    from batch" : item.ProductName;

                itemGroup.Rows.Add(sale.IsTaxInvoice
                    ? Row(false, name, item.BatchNo, expiry, qty,
                          item.Mrp.ToString("0.00"), item.GstRate.ToString("0.#"), item.LineTotal.ToString("0.00"))
                    : Row(false, name, item.BatchNo, expiry, qty,
                          item.Mrp.ToString("0.00"), item.LineTotal.ToString("0.00")));
            }
        }

        items.RowGroups.Add(itemGroup);
        doc.Blocks.Add(items);

        // GST summary grouped by rate — what makes this a valid tax invoice.
        var slabs = sale.Items.GroupBy(i => i.GstRate).OrderBy(g => g.Key).ToList();
        if (sale.IsTaxInvoice && slabs.Count > 0)
        {
            doc.Blocks.Add(Text("GST SUMMARY", 7.4, FontWeights.SemiBold, Muted, topMargin: 2));

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
        totalGroup.Rows.Add(TotalRow("Gross", sale.GrossAmount.ToString("0.00")));
        if (sale.DiscountAmount > 0)
            totalGroup.Rows.Add(TotalRow("Discount", $"-{sale.DiscountAmount:0.00}"));
        if (sale.IsTaxInvoice)
        {
            totalGroup.Rows.Add(TotalRow("Taxable value", sale.TaxableAmount.ToString("0.00")));
            totalGroup.Rows.Add(TotalRow("CGST", sale.CgstAmount.ToString("0.00")));
            totalGroup.Rows.Add(TotalRow("SGST", sale.SgstAmount.ToString("0.00")));
        }
        if (sale.RoundOff != 0)
            totalGroup.Rows.Add(TotalRow("Round off", sale.RoundOff.ToString("+0.00;-0.00")));
        totals.RowGroups.Add(totalGroup);
        doc.Blocks.Add(totals);

        doc.Blocks.Add(Text($"NET PAYABLE   ₹{sale.NetAmount:0.00}", 13, FontWeights.Bold,
                            align: TextAlignment.Right, topMargin: 1));
        doc.Blocks.Add(Text($"Paid by {sale.PaymentMode}", 8, brush: Muted, align: TextAlignment.Right));
        doc.Blocks.Add(Text(FeeReceiptDocument.InWords(sale.NetAmount), 8, brush: Muted,
                            align: TextAlignment.Right, topMargin: 3));

        doc.Blocks.Add(Rule());

        if (!string.IsNullOrWhiteSpace(pharmacy.PharmacistName))
            doc.Blocks.Add(LabelValueText("Pharmacist", pharmacy.PharmacistName, 8, brush: Muted));

        if (sale.Items.Count > 0)
            doc.Blocks.Add(LabelValueText("HSN", string.Join(", ", sale.Items.Select(i => i.HsnCode).Distinct()),
                                           7.4, brush: Muted));

        // The pharmacy's own footer, set on the Pharmacy settings tab, takes
        // over from the shared Reports footer once it is typed — see
        // PharmacyProfile.FooterText.
        var footer = string.IsNullOrWhiteSpace(pharmacy.FooterText) ? theme.Footer : pharmacy.FooterText;
        if (!string.IsNullOrWhiteSpace(footer))
            doc.Blocks.Add(Text(footer, 7.4, brush: Muted, align: TextAlignment.Center, topMargin: 6));

        ApplyPrintSettings(doc, theme);
        return doc;
    }
}
