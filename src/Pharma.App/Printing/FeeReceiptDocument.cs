using System.Windows;
using System.Windows.Documents;
using Pharma.Core;
using Pharma.Data;
using static Pharma.App.Printing.DocumentBuilder;

namespace Pharma.App.Printing;

/// <summary>
/// Receipt for a consultation fee. Deliberately short — the parent needs the
/// amount, who it was paid to and a number they can quote, and nothing else.
/// Body text runs two points larger than the pharmacy bill and the
/// diagnostic bill print at, by request — every size below is the
/// document's original size plus that same +2.
/// </summary>
public static class FeeReceiptDocument
{
    private const double SizeDelta = 2;

    public static FlowDocument Build(Visit visit, ClinicProfile clinic, DocumentTheme theme, bool isReprint = false)
    {
        var doc = NewDocument();

        AddClinicHeader(doc, clinic, theme, "CASH RECEIPT");

        // Date and time sit side by side rather than one under the other,
        // in row 1 with the receipt no; patient identity in row 2; doctor,
        // speciality and the token/visit reference in row 3.
        var when = visit.FeePaidOn ?? visit.ScheduledOn;

        var grid = NewTable(1, 1, 1);
        var group = new TableRowGroup();
        group.Rows.Add(IdentityRow(SizeDelta,
            ("Receipt No", visit.FeeReceiptNo ?? "(not issued)"),
            ("Date", $"{when:dd/MM/yyyy}"),
            ("Time", $"{when:hh\\:mm tt}")));
        group.Rows.Add(IdentityRow(SizeDelta,
            ("Patient", visit.Patient.Name),
            ("Patient No", visit.Patient.PatientNo),
            ("Age / Sex", $"{visit.Patient.Age} / {visit.Patient.Gender}")));
        group.Rows.Add(IdentityRow(SizeDelta,
            ("Doctor", visit.Doctor.Name),
            ("Speciality", visit.Doctor.Speciality ?? ""),
            ("Token / Visit", $"{visit.TokenNo} · {visit.VisitNo}")));
        grid.RowGroups.Add(group);
        doc.Blocks.Add(grid);
        doc.Blocks.Add(Rule());

        var lines = NewTable(3, 0.2, 1.2);
        var lineGroup = new TableRowGroup();
        lineGroup.Rows.Add(Row(true, SizeDelta, "PARTICULARS", "", "AMOUNT"));
        lineGroup.Rows.Add(Row(false, SizeDelta,
            string.IsNullOrWhiteSpace(visit.Doctor.Speciality)
                ? "Consultation fee"
                : $"Consultation fee — {visit.Doctor.Speciality}",
            "",
            visit.Fee.ToString("0.00")));
        lines.RowGroups.Add(lineGroup);
        doc.Blocks.Add(lines);

        doc.Blocks.Add(Rule());

        doc.Blocks.Add(Text($"RECEIVED   ₹{visit.Fee:0.00}", 13 + SizeDelta, FontWeights.Bold,
                            align: TextAlignment.Right, topMargin: 1));

        doc.Blocks.Add(Text($"Paid by {visit.FeePaymentMode?.ToString() ?? "Cash"}",
                            8 + SizeDelta, brush: Muted, align: TextAlignment.Right));

        doc.Blocks.Add(Text(InWords(visit.Fee), 8 + SizeDelta, brush: Muted,
                            align: TextAlignment.Right, topMargin: 4));

        if (visit.FollowUpOn is { } follow)
            doc.Blocks.Add(Text($"Review on {follow:dd MMM yyyy}", 9 + SizeDelta, FontWeights.SemiBold, topMargin: 5));

        // A consultation fee is a service, not a medicine sale — no GST is shown
        // here on purpose. Add it only if the clinic itself registers for it.
        doc.Blocks.Add(Text("Consultation services. Fees once paid are not refundable.",
                            7.4 + SizeDelta, brush: Muted, align: TextAlignment.Center, topMargin: 8));

        // The clinic's own footer, set on the Clinic settings tab, takes over
        // from the shared Reports footer once it is typed — see
        // ClinicProfile.FooterText.
        var footer = string.IsNullOrWhiteSpace(clinic.FooterText) ? theme.Footer : clinic.FooterText;
        if (!string.IsNullOrWhiteSpace(footer))
            doc.Blocks.Add(Text(footer, 7.4 + SizeDelta, brush: Muted, align: TextAlignment.Center, topMargin: 3));

        if (isReprint)
            doc.Blocks.Add(Text("DUPLICATE", 9 + SizeDelta, FontWeights.Bold, Muted, TextAlignment.Center, topMargin: 5));

        ApplyPrintSettings(doc, theme);
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
