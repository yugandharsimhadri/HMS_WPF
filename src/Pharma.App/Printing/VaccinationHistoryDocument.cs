using System.Windows;
using System.Windows.Documents;
using Pharma.Core;
using Pharma.Data;
using static Pharma.App.Printing.DocumentBuilder;

namespace Pharma.App.Printing;

/// <summary>
/// A patient's vaccination record, printable as a simple certificate-style
/// list — what a parent hands to a school or asks for before travel.
/// </summary>
public static class VaccinationHistoryDocument
{
    public static FlowDocument Build(Patient patient, IReadOnlyList<VaccinationRecord> records, ClinicProfile clinic, DocumentTheme theme)
    {
        var doc = NewDocument();

        AddClinicHeader(doc, clinic, theme, "VACCINATION RECORD");

        var head = NewTable(1, 1, 1);
        var headGroup = new TableRowGroup();
        headGroup.Rows.Add(IdentityRow(
            ("Patient", patient.Name),
            ("Patient No", patient.PatientNo),
            ("Age", $"{patient.Age} yrs")));
        headGroup.Rows.Add(IdentityRow(
            ("Gender", patient.Gender.ToString()),
            ("Guardian", patient.GuardianName ?? "-"),
            ("Printed on", $"{DateTime.Now:dd/MM/yyyy}")));
        head.RowGroups.Add(headGroup);
        doc.Blocks.Add(head);
        doc.Blocks.Add(Rule());

        var table = NewTable(1, 1.8, 1.8, 0.6, 1, 1.2);
        var group = new TableRowGroup();
        group.Rows.Add(Row(true, "GIVEN ON", "TYPE", "BRAND GIVEN", "DOSE", "BATCH", "SITE"));

        foreach (var record in records.OrderBy(r => r.GivenOn))
        {
            var brand = record.ProductName is { } product
                ? (record.Manufacturer is { } maker ? $"{product} ({maker})" : product)
                : "-";

            group.Rows.Add(Row(false,
                $"{record.GivenOn:dd/MM/yyyy}", record.VaccineName, brand, record.DoseNumber.ToString(),
                record.BatchNo ?? "-", record.SiteOfInjection ?? "-"));
        }

        table.RowGroups.Add(group);
        doc.Blocks.Add(table);

        if (records.Count == 0)
            doc.Blocks.Add(Text("No doses recorded yet.", 9, brush: Muted, topMargin: 6));

        doc.Blocks.Add(Rule());

        var footer = string.IsNullOrWhiteSpace(clinic.FooterText) ? theme.Footer : clinic.FooterText;
        if (!string.IsNullOrWhiteSpace(footer))
            doc.Blocks.Add(Text(footer, 7.4, brush: Muted, align: TextAlignment.Center, topMargin: 6));

        ApplyPrintSettings(doc, theme);
        return doc;
    }
}
