using System.Windows;
using System.Windows.Documents;
using Pharma.Core;
using Pharma.Data;
using static Pharma.App.Printing.DocumentBuilder;

namespace Pharma.App.Printing;

/// <summary>
/// The procedure bill — Pediatrics today, Dentist's simple billing later.
/// Deliberately near-identical to <see cref="DiagnosticBillPrinter"/>: a
/// list of priced items, a total, and who paid, no GST or batch involved.
/// </summary>
public static class ProcedureBillDocument
{
    public static FlowDocument Build(
        ProcedureBill bill, ClinicProfile clinic, DocumentTheme theme, bool isReprint = false)
    {
        var doc = NewDocument();

        AddClinicHeader(doc, clinic, theme, isReprint ? "PROCEDURE BILL (DUPLICATE)" : "PROCEDURE BILL");

        var head = NewTable(1, 1, 1);
        var headGroup = new TableRowGroup();
        headGroup.Rows.Add(IdentityRow(
            ("Bill No", bill.BillNo),
            ("Date", $"{bill.BillDate:dd/MM/yyyy}"),
            ("Time", $"{bill.BillDate:HH:mm}")));
        headGroup.Rows.Add(IdentityRow(
            ("Patient", bill.PatientName),
            ("Patient No", bill.PatientNo),
            ("Status", bill.Status.ToString())));

        if (!string.IsNullOrWhiteSpace(bill.ReferredBy))
            headGroup.Rows.Add(IdentityRow(("Referred by", bill.ReferredBy), ("Payment", bill.PaymentMode.ToString())));
        else
            headGroup.Rows.Add(IdentityRow(("Payment", bill.PaymentMode.ToString())));

        head.RowGroups.Add(headGroup);
        doc.Blocks.Add(head);
        doc.Blocks.Add(Rule());

        var items = NewTable(3.6, 0.8, 1.0, 1.2);
        var itemGroup = new TableRowGroup();
        itemGroup.Rows.Add(Row(true, "PROCEDURE", "QTY", "PRICE", "AMOUNT"));

        foreach (var item in bill.Items)
            itemGroup.Rows.Add(Row(false, item.ProcedureName, item.Quantity.ToString(),
                item.Price.ToString("0.00"), item.Amount.ToString("0.00")));

        items.RowGroups.Add(itemGroup);
        doc.Blocks.Add(items);
        doc.Blocks.Add(Rule());

        var totals = NewTable(3, 0.2, 1.2);
        var totalGroup = new TableRowGroup();
        totalGroup.Rows.Add(TotalRow("Total", bill.TotalAmount.ToString("0.00")));
        if (bill.Discount > 0)
            totalGroup.Rows.Add(TotalRow("Discount", $"-{bill.Discount:0.00}"));
        totals.RowGroups.Add(totalGroup);
        doc.Blocks.Add(totals);

        doc.Blocks.Add(Text($"AMOUNT PAYABLE   ₹{bill.FinalAmount:0.00}", 13, FontWeights.Bold,
                            align: TextAlignment.Right, topMargin: 1));
        doc.Blocks.Add(Text($"Paid by {bill.PaymentMode}", 8, brush: Muted, align: TextAlignment.Right));
        doc.Blocks.Add(Text(FeeReceiptDocument.InWords(bill.FinalAmount), 8, brush: Muted,
                            align: TextAlignment.Right, topMargin: 3));

        doc.Blocks.Add(Rule());

        var footer = string.IsNullOrWhiteSpace(clinic.FooterText) ? theme.Footer : clinic.FooterText;
        if (!string.IsNullOrWhiteSpace(footer))
            doc.Blocks.Add(Text(footer, 7.4, brush: Muted, align: TextAlignment.Center, topMargin: 6));

        if (isReprint)
            doc.Blocks.Add(Text("DUPLICATE", 9, FontWeights.Bold, Muted, TextAlignment.Center, topMargin: 5));

        ApplyPrintSettings(doc, theme);
        return doc;
    }
}
