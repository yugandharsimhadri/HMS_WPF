using System.Windows.Documents;
using Pharma.Core;
using Pharma.Data;
using static Pharma.App.Printing.DocumentBuilder;

namespace Pharma.App.Printing;

/// <summary>
/// A printed confirmation slip for a booked-ahead appointment — deliberately
/// short, the same reasoning <see cref="FeeReceiptDocument"/> gives for its
/// own brevity: the patient needs when, with whom, and a number they can
/// quote if they call to change it, and nothing else.
/// </summary>
public static class AppointmentSlipDocument
{
    public static FlowDocument Build(Appointment appointment, ClinicProfile clinic, DocumentTheme theme)
    {
        var doc = NewDocument();

        AddClinicHeader(doc, clinic, theme, "APPOINTMENT CONFIRMATION");

        var grid = NewTable(1, 1, 1);
        var group = new TableRowGroup();
        group.Rows.Add(IdentityRow(
            ("Appointment No", appointment.AppointmentNo),
            ("Date", $"{appointment.ScheduledOn:dd/MM/yyyy}"),
            ("Time", $"{appointment.ScheduledOn:hh\\:mm tt}")));
        group.Rows.Add(IdentityRow(
            ("Patient", appointment.PatientName),
            ("Doctor", appointment.DoctorName),
            ("Phone", appointment.PatientPhone)));
        grid.RowGroups.Add(group);
        doc.Blocks.Add(grid);
        doc.Blocks.Add(Rule());

        if (!string.IsNullOrWhiteSpace(appointment.Reason))
            doc.Blocks.Add(Text($"Reason: {appointment.Reason}", 9, topMargin: 6));

        doc.Blocks.Add(Text(
            "Please arrive a few minutes early. Call the clinic to cancel or reschedule.",
            7.4, brush: Muted, topMargin: 8));

        var footer = string.IsNullOrWhiteSpace(clinic.FooterText) ? theme.Footer : clinic.FooterText;
        if (!string.IsNullOrWhiteSpace(footer))
            doc.Blocks.Add(Text(footer, 7.4, brush: Muted, topMargin: 3));

        ApplyPrintSettings(doc, theme);
        return doc;
    }
}
