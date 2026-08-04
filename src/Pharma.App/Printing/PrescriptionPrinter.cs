using System.Windows;
using System.Windows.Documents;
using Pharma.Core;
using Pharma.Data;
using static Pharma.App.Printing.DocumentBuilder;

namespace Pharma.App.Printing;

/// <summary>
/// OPD prescription slip — boxed clinic header, three-row identity grid, Rx
/// table, follow-up. Body text runs two points larger than the pharmacy
/// bill and the diagnostic bill print at, by request — every size below is
/// the document's original size plus that same +2.
/// </summary>
public static class PrescriptionPrinter
{
    private const double SizeDelta = 2;

    public static FlowDocument Build(Visit visit, ClinicProfile clinic, DocumentTheme theme)
    {
        var doc = NewDocument();

        AddClinicHeader(doc, clinic, theme, null);

        doc.Blocks.Add(Text(visit.Doctor.Name, 10 + SizeDelta, FontWeights.SemiBold, align: TextAlignment.Center));

        var credentials = new List<string>();
        if (!string.IsNullOrWhiteSpace(visit.Doctor.Speciality)) credentials.Add(visit.Doctor.Speciality!);
        if (!string.IsNullOrWhiteSpace(visit.Doctor.RegistrationNo)) credentials.Add($"Reg. No: {visit.Doctor.RegistrationNo}");
        if (credentials.Count > 0)
            doc.Blocks.Add(Text(string.Join("   |   ", credentials), 8 + SizeDelta, brush: Muted, align: TextAlignment.Center));

        // Date and time sit side by side rather than one under the other,
        // in row 1 with the visit no; patient identity in row 2; doctor,
        // speciality and the token in row 3.
        var grid = NewTable(1, 1, 1);
        var group = new TableRowGroup();
        group.Rows.Add(IdentityRow(SizeDelta,
            ("Visit No", visit.VisitNo),
            ("Date", $"{visit.ScheduledOn:dd/MM/yyyy}"),
            ("Time", $"{visit.ScheduledOn:hh\\:mm tt}")));
        group.Rows.Add(IdentityRow(SizeDelta,
            ("Patient", visit.Patient.Name),
            ("Patient No", visit.Patient.PatientNo),
            ("Age / Sex", $"{visit.Patient.Age} / {visit.Patient.Gender}")));
        group.Rows.Add(IdentityRow(SizeDelta,
            ("Doctor", visit.Doctor.Name),
            ("Speciality", visit.Doctor.Speciality ?? ""),
            ("Token", visit.TokenNo.ToString())));
        grid.RowGroups.Add(group);
        doc.Blocks.Add(grid);
        doc.Blocks.Add(Rule());

        var vitals = new List<string>();
        if (visit.WeightKg is { } w) vitals.Add($"Wt {w:0.#} kg");
        if (!string.IsNullOrWhiteSpace(visit.BloodPressure)) vitals.Add($"BP {visit.BloodPressure}");
        if (visit.TemperatureF is { } t) vitals.Add($"Temp {t:0.#} °F");
        if (vitals.Count > 0)
            doc.Blocks.Add(Text(string.Join("   ·   ", vitals), 8 + SizeDelta, brush: Muted));

        if (!string.IsNullOrWhiteSpace(visit.Complaint))
            doc.Blocks.Add(LabelValueText("Complaint", visit.Complaint, 8.5 + SizeDelta, topMargin: 3));

        if (!string.IsNullOrWhiteSpace(visit.Diagnosis))
            doc.Blocks.Add(LabelValueText("Diagnosis", visit.Diagnosis, 8.5 + SizeDelta));

        doc.Blocks.Add(Text("Rx", 15 + SizeDelta, FontWeights.Bold, topMargin: 6, bottomMargin: 1));

        if (visit.Prescription.Count > 0)
        {
            var table = NewTable(3.4, 1.4, 1.2, 0.8, 0.8);
            var rows = new TableRowGroup();
            rows.Rows.Add(Row(true, SizeDelta, "MEDICINE", "DOSE", "FREQUENCY", "DAYS", "QTY"));

            foreach (var item in visit.Prescription)
            {
                rows.Rows.Add(Row(false, SizeDelta,
                    item.MedicineName,
                    item.Dosage ?? "",
                    item.Frequency ?? "",
                    item.Days > 0 ? item.Days.ToString() : "",
                    item.Quantity > 0 ? item.Quantity.ToString() : ""));

                if (!string.IsNullOrWhiteSpace(item.Instructions))
                    rows.Rows.Add(Row(false, SizeDelta, $"    {item.Instructions}", "", "", "", ""));
            }

            table.RowGroups.Add(rows);
            doc.Blocks.Add(table);
        }
        else
        {
            doc.Blocks.Add(Text("No medicines prescribed.", 8.5 + SizeDelta, brush: Muted));
        }

        if (visit.DiagnosticRequests.Count > 0)
        {
            doc.Blocks.Add(Text("Investigations advised", 12 + SizeDelta, FontWeights.Bold, topMargin: 8, bottomMargin: 1));

            var testTable = NewTable(1);
            var testRows = new TableRowGroup();
            testRows.Rows.Add(Row(true, SizeDelta, "TEST"));

            foreach (var request in visit.DiagnosticRequests)
                testRows.Rows.Add(Row(false, SizeDelta, request.TestName));

            testTable.RowGroups.Add(testRows);
            doc.Blocks.Add(testTable);
        }

        if (!string.IsNullOrWhiteSpace(visit.Notes))
            doc.Blocks.Add(Text($"Advice: {visit.Notes}", 8.5 + SizeDelta, topMargin: 4));

        if (visit.FollowUpOn is { } follow)
            doc.Blocks.Add(Text($"Review on {follow:dd MMM yyyy}", 9 + SizeDelta, FontWeights.SemiBold, topMargin: 4));

        // The clinic's own footer, set on the Clinic settings tab, takes over
        // from the shared Reports footer once it is typed — see
        // ClinicProfile.FooterText.
        var footer = string.IsNullOrWhiteSpace(clinic.FooterText) ? theme.Footer : clinic.FooterText;
        if (!string.IsNullOrWhiteSpace(footer))
            doc.Blocks.Add(Text(footer, 7.6 + SizeDelta, brush: Muted, align: TextAlignment.Center, topMargin: 6));

        doc.Blocks.Add(Text(visit.Doctor.Name, 8.5 + SizeDelta, FontWeights.SemiBold,
                            align: TextAlignment.Right, topMargin: 20));
        doc.Blocks.Add(Text("Signature", 7.4 + SizeDelta, brush: Muted, align: TextAlignment.Right));

        ApplyPrintSettings(doc, theme);
        return doc;
    }
}
