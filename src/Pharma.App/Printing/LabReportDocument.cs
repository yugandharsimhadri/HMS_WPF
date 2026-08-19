using System.Windows;
using System.Windows.Documents;
using Pharma.Core;
using Pharma.Data;
using static Pharma.App.Printing.DocumentBuilder;

namespace Pharma.App.Printing;

/// <summary>
/// The pathology lab report — one section per ordered report, each listing
/// its analytes' results against the reference range snapshotted at entry
/// time. Only ever printed once the order has been verified — see
/// <see cref="PathologyLabViewModel.PrintReportAsync"/>, which is where that
/// is enforced; this builder itself just renders whatever it is given.
/// </summary>
public static class LabReportDocument
{
    public static FlowDocument Build(LabOrder order, ClinicProfile clinic, DocumentTheme theme)
    {
        var doc = NewDocument();

        AddClinicHeader(doc, clinic, theme, "LABORATORY REPORT");

        var head = NewTable(1, 1, 1);
        var headGroup = new TableRowGroup();
        headGroup.Rows.Add(IdentityRow(
            ("Order No", order.OrderNo),
            ("Date", $"{order.OrderDate:dd/MM/yyyy}"),
            ("Time", $"{order.OrderDate:HH:mm}")));
        headGroup.Rows.Add(IdentityRow(
            ("Patient", order.PatientName),
            ("Patient No", order.PatientNo),
            ("Referred By", order.ReferredBy ?? "-")));

        if (!string.IsNullOrWhiteSpace(order.SpecimenId) || order.CollectedOn is not null)
            headGroup.Rows.Add(IdentityRow(
                ("Specimen", order.SpecimenId ?? "-"),
                ("Collected", order.CollectedOn is { } collected ? $"{collected:dd/MM/yyyy HH:mm}" : "-"),
                ("Received", order.ReceivedOn is { } received ? $"{received:dd/MM/yyyy HH:mm}" : "-")));

        head.RowGroups.Add(headGroup);
        doc.Blocks.Add(head);
        doc.Blocks.Add(Rule());

        foreach (var orderReport in order.Reports.OrderBy(r => r.ReportName))
        {
            doc.Blocks.Add(Text(orderReport.ReportName.ToUpperInvariant(), 10, FontWeights.Bold, topMargin: 6, bottomMargin: 2));

            var table = NewTable(2.2, 1, 0.8, 1.6, 0.8);
            var group = new TableRowGroup();
            group.Rows.Add(Row(true, "ANALYTE", "RESULT", "UNITS", "REFERENCE RANGE", "FLAG"));

            foreach (var result in orderReport.Results.OrderBy(r => r.AnalyteName))
            {
                var flagText = result.Flag == LabResultFlag.Normal ? "" : result.Flag.ToString().ToUpperInvariant();
                group.Rows.Add(Row(false, result.AnalyteName, result.ResultValue, result.Units, result.ReferenceRangeDisplay, flagText));
            }

            table.RowGroups.Add(group);
            doc.Blocks.Add(table);

            if (!string.IsNullOrWhiteSpace(orderReport.Notes))
                doc.Blocks.Add(Text($"Interpretation: {orderReport.Notes}", 8, brush: Muted, topMargin: 2));
        }

        doc.Blocks.Add(Rule());

        var totals = NewTable(3, 0.2, 1.2);
        var totalGroup = new TableRowGroup();
        totalGroup.Rows.Add(TotalRow("Total", order.TotalAmount.ToString("0.00")));
        if (order.Discount > 0) totalGroup.Rows.Add(TotalRow("Discount", $"-{order.Discount:0.00}"));
        totals.RowGroups.Add(totalGroup);
        doc.Blocks.Add(totals);

        doc.Blocks.Add(Text($"AMOUNT PAID   ₹{order.FinalAmount:0.00}", 13, FontWeights.Bold,
                            align: TextAlignment.Right, topMargin: 1));
        doc.Blocks.Add(Text($"Paid by {order.PaymentMode}", 8, brush: Muted, align: TextAlignment.Right));

        if (!string.IsNullOrWhiteSpace(order.Remarks))
            doc.Blocks.Add(Text($"Remarks: {order.Remarks}", 8, brush: Muted, topMargin: 3));

        doc.Blocks.Add(Rule());

        var verifiedOn = order.Reports.SelectMany(r => r.Results).Select(r => r.VerifiedOn).Where(v => v is not null).Max();
        var verifiedBy = order.Reports.SelectMany(r => r.Results).Select(r => r.VerifiedBy).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        if (verifiedOn is not null)
            doc.Blocks.Add(Text($"Verified by {verifiedBy} on {verifiedOn:dd/MM/yyyy HH:mm}", 7.6, brush: Muted,
                                align: TextAlignment.Right, topMargin: 4));

        var footer = string.IsNullOrWhiteSpace(clinic.FooterText) ? theme.Footer : clinic.FooterText;
        if (!string.IsNullOrWhiteSpace(footer))
            doc.Blocks.Add(Text(footer, 7.4, brush: Muted, align: TextAlignment.Center, topMargin: 6));

        ApplyPrintSettings(doc, theme);
        return doc;
    }
}
