using System.Windows;
using System.Windows.Documents;
using Pharma.Core;
using Pharma.Data;
using static Pharma.App.Printing.DocumentBuilder;

namespace Pharma.App.Printing;

/// <summary>OPD prescription slip — boxed clinic header, three-row identity grid, Rx table, follow-up.</summary>
public static class PrescriptionPrinter
{
    public static FlowDocument Build(Visit visit, ClinicProfile clinic, DocumentTheme theme)
    {
        var doc = NewDocument();

        AddClinicHeader(doc, clinic, theme, null);

        doc.Blocks.Add(Text(visit.Doctor.Name, 10, FontWeights.SemiBold, align: TextAlignment.Center));

        var credentials = new List<string>();
        if (!string.IsNullOrWhiteSpace(visit.Doctor.Speciality)) credentials.Add(visit.Doctor.Speciality!);
        if (!string.IsNullOrWhiteSpace(visit.Doctor.RegistrationNo)) credentials.Add($"Reg. No: {visit.Doctor.RegistrationNo}");
        if (credentials.Count > 0)
            doc.Blocks.Add(Text(string.Join("   |   ", credentials), 8, brush: Muted, align: TextAlignment.Center));

        var grid = NewTable(1, 1, 1);
        var group = new TableRowGroup();
        group.Rows.Add(IdentityRow(
            ("Visit No", visit.VisitNo), ("Date", $"{visit.ScheduledOn:dd/MM/yyyy}"),
            ("Patient", visit.Patient.Name)));
        group.Rows.Add(IdentityRow(
            ("Patient No", visit.Patient.PatientNo),
            ("Age / Sex", $"{visit.Patient.Age} / {visit.Patient.Gender}"),
            ("Doctor", visit.Doctor.Name)));
        group.Rows.Add(IdentityRow(
            ("Token", visit.TokenNo.ToString()), ("Time", $"{visit.ScheduledOn:hh\\:mm tt}"),
            ("Speciality", visit.Doctor.Speciality ?? "")));
        grid.RowGroups.Add(group);
        doc.Blocks.Add(grid);
        doc.Blocks.Add(Rule());

        var vitals = new List<string>();
        if (visit.WeightKg is { } w) vitals.Add($"Wt {w:0.#} kg");
        if (!string.IsNullOrWhiteSpace(visit.BloodPressure)) vitals.Add($"BP {visit.BloodPressure}");
        if (visit.TemperatureF is { } t) vitals.Add($"Temp {t:0.#} °F");
        if (vitals.Count > 0)
            doc.Blocks.Add(Text(string.Join("   ·   ", vitals), 8, brush: Muted));

        if (!string.IsNullOrWhiteSpace(visit.Complaint))
            doc.Blocks.Add(Text($"Complaint: {visit.Complaint}", 8.5, topMargin: 3));

        if (!string.IsNullOrWhiteSpace(visit.Diagnosis))
            doc.Blocks.Add(Text($"Diagnosis: {visit.Diagnosis}", 8.5, FontWeights.SemiBold));

        doc.Blocks.Add(Text("Rx", 15, FontWeights.Bold, topMargin: 6, bottomMargin: 1));

        if (visit.Prescription.Count > 0)
        {
            var table = NewTable(3.4, 1.4, 1.2, 0.8, 0.8);
            var rows = new TableRowGroup();
            rows.Rows.Add(Row(true, "MEDICINE", "DOSE", "FREQUENCY", "DAYS", "QTY"));

            foreach (var item in visit.Prescription)
            {
                rows.Rows.Add(Row(false,
                    item.MedicineName,
                    item.Dosage ?? "",
                    item.Frequency ?? "",
                    item.Days > 0 ? item.Days.ToString() : "",
                    item.Quantity > 0 ? item.Quantity.ToString() : ""));

                if (!string.IsNullOrWhiteSpace(item.Instructions))
                    rows.Rows.Add(Row(false, $"    {item.Instructions}", "", "", "", ""));
            }

            table.RowGroups.Add(rows);
            doc.Blocks.Add(table);
        }
        else
        {
            doc.Blocks.Add(Text("No medicines prescribed.", 8.5, brush: Muted));
        }

        if (!string.IsNullOrWhiteSpace(visit.Notes))
            doc.Blocks.Add(Text($"Advice: {visit.Notes}", 8.5, topMargin: 4));

        if (visit.FollowUpOn is { } follow)
            doc.Blocks.Add(Text($"Review on {follow:dd MMM yyyy}", 9, FontWeights.SemiBold, topMargin: 4));

        if (!string.IsNullOrWhiteSpace(theme.Footer))
            doc.Blocks.Add(Text(theme.Footer, 7.6, brush: Muted, align: TextAlignment.Center, topMargin: 6));

        doc.Blocks.Add(Text(visit.Doctor.Name, 8.5, FontWeights.SemiBold,
                            align: TextAlignment.Right, topMargin: 20));
        doc.Blocks.Add(Text("Signature", 7.4, brush: Muted, align: TextAlignment.Right));

        return doc;
    }
}
