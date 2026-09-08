using System.Windows;
using System.Windows.Documents;
using Pharma.Core;
using Pharma.Data;
using static Pharma.App.Printing.DocumentBuilder;

namespace Pharma.App.Printing;

/// <summary>
/// Receipt for one payment against a <see cref="DentalCase"/>'s running
/// balance — printed per <see cref="DentalPayment"/>, not once per case,
/// since a case is typically paid across several visits. Shows the case's
/// balance still due so a partial-payment workflow is clear from the first
/// receipt onward.
/// </summary>
public static class DentalReceiptDocument
{
    public static FlowDocument Build(DentalPayment payment, DentalCase dentalCase, ClinicProfile clinic, DocumentTheme theme)
    {
        var doc = NewDocument();

        AddClinicHeader(doc, clinic, theme, "DENTAL PAYMENT RECEIPT");

        var grid = NewTable(1, 1, 1);
        var group = new TableRowGroup();
        group.Rows.Add(IdentityRow(
            ("Receipt No", payment.ReceiptNo),
            ("Date", $"{payment.PaidOn:dd/MM/yyyy}"),
            ("Time", $"{payment.PaidOn:HH:mm}")));
        group.Rows.Add(IdentityRow(
            ("Patient", dentalCase.PatientName),
            ("Procedure / Package", dentalCase.ProcedureName ?? dentalCase.PackageName ?? ""),
            ("Tooth", dentalCase.ToothNumber ?? "")));
        grid.RowGroups.Add(group);
        doc.Blocks.Add(grid);
        doc.Blocks.Add(Rule());

        doc.Blocks.Add(Text($"AMOUNT RECEIVED   ₹{payment.Amount:0.00}", 13, FontWeights.Bold,
                            align: TextAlignment.Right, topMargin: 1));
        doc.Blocks.Add(Text($"Paid by {payment.PaymentMode}", 8, brush: Muted, align: TextAlignment.Right));
        doc.Blocks.Add(Text(FeeReceiptDocument.InWords(payment.Amount), 8, brush: Muted,
                            align: TextAlignment.Right, topMargin: 3));

        doc.Blocks.Add(Rule());

        var totals = NewTable(3, 0.2, 1.2);
        var totalGroup = new TableRowGroup();
        totalGroup.Rows.Add(TotalRow("Case total", dentalCase.TotalCost.ToString("0.00")));
        totalGroup.Rows.Add(TotalRow("Paid to date", dentalCase.AmountPaid.ToString("0.00")));
        totals.RowGroups.Add(totalGroup);
        doc.Blocks.Add(totals);

        doc.Blocks.Add(Text($"BALANCE DUE   ₹{dentalCase.Balance:0.00}", 11, FontWeights.Bold,
                            brush: dentalCase.Balance > 0 ? Muted : null,
                            align: TextAlignment.Right, topMargin: 4));

        doc.Blocks.Add(Rule());

        var footer = string.IsNullOrWhiteSpace(clinic.FooterText) ? theme.Footer : clinic.FooterText;
        if (!string.IsNullOrWhiteSpace(footer))
            doc.Blocks.Add(Text(footer, 7.4, brush: Muted, align: TextAlignment.Center, topMargin: 6));

        ApplyPrintSettings(doc, theme);
        return doc;
    }
}
