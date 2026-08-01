using System.Windows;
using System.Windows.Documents;
using Pharma.Core;
using Pharma.Data;
using static Pharma.App.Printing.DocumentBuilder;

namespace Pharma.App.Printing;

/// <summary>
/// Receipt for a consultation fee. Deliberately short — the parent needs the
/// amount, who it was paid to and a number they can quote, and nothing else.
/// </summary>
public static class FeeReceiptDocument
{
    public static FlowDocument Build(Visit visit, ClinicProfile clinic, DocumentTheme theme, bool isReprint = false)
    {
        var doc = NewDocument();

        AddClinicHeader(doc, clinic, theme, "CASH RECEIPT");

        var grid = NewTable(1, 1, 1);
        var group = new TableRowGroup();
        group.Rows.Add(IdentityRow(
            ("Receipt No", visit.FeeReceiptNo ?? "(not issued)"),
            ("Date", $"{(visit.FeePaidOn ?? visit.ScheduledOn):dd/MM/yyyy}"),
            ("Patient", visit.Patient.Name)));
        group.Rows.Add(IdentityRow(
            ("Patient No", visit.Patient.PatientNo),
            ("Age / Sex", $"{visit.Patient.Age} / {visit.Patient.Gender}"),
            ("Doctor", visit.Doctor.Name)));
        group.Rows.Add(IdentityRow(
            ("Token / Visit", $"{visit.TokenNo} · {visit.VisitNo}"),
            ("Time", $"{(visit.FeePaidOn ?? visit.ScheduledOn):hh\\:mm tt}"),
            ("Speciality", visit.Doctor.Speciality ?? "")));
        grid.RowGroups.Add(group);
        doc.Blocks.Add(grid);
        doc.Blocks.Add(Rule());

        var lines = NewTable(3, 0.2, 1.2);
        var lineGroup = new TableRowGroup();
        lineGroup.Rows.Add(Row(true, "PARTICULARS", "", "AMOUNT"));
        lineGroup.Rows.Add(Row(false,
            string.IsNullOrWhiteSpace(visit.Doctor.Speciality)
                ? "Consultation fee"
                : $"Consultation fee — {visit.Doctor.Speciality}",
            "",
            visit.Fee.ToString("0.00")));
        lines.RowGroups.Add(lineGroup);
        doc.Blocks.Add(lines);

        doc.Blocks.Add(Rule());

        doc.Blocks.Add(Text($"RECEIVED   ₹{visit.Fee:0.00}", 13, FontWeights.Bold,
                            align: TextAlignment.Right, topMargin: 1));

        doc.Blocks.Add(Text($"Paid by {visit.FeePaymentMode?.ToString() ?? "Cash"}",
                            8, brush: Muted, align: TextAlignment.Right));

        doc.Blocks.Add(Text(InWords(visit.Fee), 8, brush: Muted, topMargin: 4));

        if (visit.FollowUpOn is { } follow)
            doc.Blocks.Add(Text($"Review on {follow:dd MMM yyyy}", 9, FontWeights.SemiBold, topMargin: 5));

        // A consultation fee is a service, not a medicine sale — no GST is shown
        // here on purpose. Add it only if the clinic itself registers for it.
        doc.Blocks.Add(Text("Consultation services. Fees once paid are not refundable.",
                            7.4, brush: Muted, align: TextAlignment.Center, topMargin: 8));

        if (!string.IsNullOrWhiteSpace(theme.Footer))
            doc.Blocks.Add(Text(theme.Footer, 7.4, brush: Muted, align: TextAlignment.Center, topMargin: 3));

        if (isReprint)
            doc.Blocks.Add(Text("DUPLICATE", 9, FontWeights.Bold, Muted, TextAlignment.Center, topMargin: 5));

        return doc;
    }

    /// <summary>Rupees in words, as an Indian receipt is expected to carry.</summary>
    public static string InWords(decimal amount)
    {
        var rupees = (long)Math.Floor(amount);
        var paise = (int)Math.Round((amount - rupees) * 100, MidpointRounding.AwayFromZero);

        var words = rupees == 0 ? "Zero" : Convert(rupees);
        var text = $"Rupees {words}";

        if (paise > 0) text += $" and {Convert(paise)} Paise";

        return text + " only";
    }

    private static string Convert(long value)
    {
        if (value == 0) return "";

        // Indian grouping: crore, lakh, thousand, hundred.
        if (value >= 10_000_000) return $"{Convert(value / 10_000_000)} Crore {Convert(value % 10_000_000)}".Trim();
        if (value >= 100_000) return $"{Convert(value / 100_000)} Lakh {Convert(value % 100_000)}".Trim();
        if (value >= 1_000) return $"{Convert(value / 1_000)} Thousand {Convert(value % 1_000)}".Trim();
        if (value >= 100) return $"{Convert(value / 100)} Hundred {Convert(value % 100)}".Trim();

        string[] ones =
        [
            "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
            "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen",
            "Eighteen", "Nineteen"
        ];

        string[] tens =
        [
            "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
        ];

        if (value < 20) return ones[value];
        return $"{tens[value / 10]} {ones[value % 10]}".Trim();
    }
}
