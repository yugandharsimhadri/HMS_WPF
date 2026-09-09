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

        // Two rows of four: what this receipt is, then who it is about.
        //
        // Date and time are one field because they are one fact — nobody reads
        // the hour without the day. The speciality is gone from here; it still
        // names the consultation on the particulars line below, where it is
        // doing some work, rather than being repeated as a label of its own.
        //
        // Neither the visit number nor the patient number is printed. Both are
        // internal references the parent has no use for and cannot act on; what
        // they need to quote is the receipt number, which row 1 carries.
        //
        // Column widths are shared down the grid, so they are set for the
        // longest thing in each column — column three carries the doctor's name
        // and degrees and needs the room; column two carries the date and time.
        var when = visit.FeePaidOn ?? visit.ScheduledOn;

        var grid = NewTable(1.0, 1.1, 1.4, 0.9);
        var group = new TableRowGroup();

        // A review is never given a receipt number of its own. No money is
        // taken, and burning an RCP number on a nil amount would put a hole in
        // the day's collection when the receipts are reconciled. It carries the
        // number the fee was actually taken on instead, under the same label —
        // so a review slip and the receipt it rides on quote the same number,
        // and the Visit line and the nil amount are what tell them apart.
        var receiptNo = visit.IsReview
            ? visit.FeeWaivedAgainstVisit?.FeeReceiptNo ?? "—"
            : visit.FeeReceiptNo ?? "(not issued)";

        // The token is the patient's place in the day's queue, and it is on the
        // receipt at the clinic's request: it is what the desk and the parent
        // both used to refer to the visit while they were in the building.
        group.Rows.Add(IdentityRow(SizeDelta,
            ("Receipt No", receiptNo),
            ("Date & time", $"{when:dd/MM/yyyy}  {when:hh\\:mm tt}"),
            ("Visit", visit.VisitKind),
            ("Token No", visit.TokenNo.ToString())));

        // Degrees beside the name and the registration under its own label — a
        // receipt is a medico-legal document, and who gave the consultation is
        // what it is expected to state plainly.
        group.Rows.Add(IdentityRow(SizeDelta,
            ("Patient", visit.Patient.Name),
            ("Age / Sex", $"{visit.Patient.Age} / {visit.Patient.Gender}"),
            ("Doctor", visit.Doctor.NameWithQualification),
            ("Reg. No", visit.Doctor.RegistrationNo ?? "—")));

        grid.RowGroups.Add(group);
        doc.Blocks.Add(grid);
        doc.Blocks.Add(Rule());

        var lines = NewTable(3, 0.2, 1.2);
        var lineGroup = new TableRowGroup();
        lineGroup.Rows.Add(Row(true, SizeDelta, "PARTICULARS", "", "AMOUNT"));
        var particulars = string.IsNullOrWhiteSpace(visit.Doctor.Speciality)
            ? "Consultation fee"
            : $"Consultation fee — {visit.Doctor.Speciality}";

        lineGroup.Rows.Add(Row(false, SizeDelta,
            visit.IsReview ? $"{particulars} (review — already paid)" : particulars,
            "",
            visit.Fee.ToString("0.00")));
        lines.RowGroups.Add(lineGroup);
        doc.Blocks.Add(lines);

        doc.Blocks.Add(Rule());

        // Nil on a review, and said out loud rather than left blank — a receipt
        // with no amount on it invites the question the figure answers.
        doc.Blocks.Add(Text($"RECEIVED   ₹{visit.Fee:0.00}", 13 + SizeDelta, FontWeights.Bold,
                            align: TextAlignment.Right, topMargin: 1));

        if (visit.IsReview)
        {
            var paidOn = visit.FeeWaivedAgainstVisit?.FeePaidOn;
            doc.Blocks.Add(Text(
                paidOn is { } on
                    ? $"No fee — already paid on {on:dd MMM yyyy}"
                    : "No fee — already paid",
                8 + SizeDelta, brush: Muted, align: TextAlignment.Right));
        }
        else
        {
            doc.Blocks.Add(Text($"Paid by {visit.FeePaymentMode?.ToString() ?? "Cash"}",
                                8 + SizeDelta, brush: Muted, align: TextAlignment.Right));
        }

        doc.Blocks.Add(Text(InWords(visit.Fee), 8 + SizeDelta, brush: Muted,
                            align: TextAlignment.Right, topMargin: 4));

        // Says the date, not a number of days to count. This is the line the
        // desk points at when a parent asks whether they have to pay again.
        if (!visit.IsReview && visit.FreeFollowUpUntil is { } coverUntil)
            doc.Blocks.Add(Text(
                $"Free review with {visit.Doctor.Name} until {coverUntil:dd MMM yyyy}",
                9 + SizeDelta, FontWeights.SemiBold, topMargin: 5));

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
