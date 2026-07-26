using System.Windows;
using System.Windows.Documents;
using Pharma.Core;
using Pharma.Data;
using static Pharma.App.Printing.DocumentBuilder;

namespace Pharma.App.Printing;

/// <summary>OPD prescription slip — clinic header, patient line, vitals, Rx table, follow-up.</summary>
public static class PrescriptionPrinter
{
    public static void Print(Visit visit, ShopProfile shop)
        => Send(Build(visit, shop), $"Prescription {visit.VisitNo}");

    public static FlowDocument Build(Visit visit, ShopProfile shop)
    {
        var doc = NewDocument();

        doc.Blocks.Add(Text(shop.Name, 18, FontWeights.Bold, align: TextAlignment.Center));

        var identity = new List<string>();
        if (!string.IsNullOrWhiteSpace(shop.AddressLine)) identity.Add(shop.AddressLine);
        if (!string.IsNullOrWhiteSpace(shop.Phone)) identity.Add($"Ph {shop.Phone}");
        if (identity.Count > 0)
            doc.Blocks.Add(Text(string.Join("  ·  ", identity), 10, brush: Muted, align: TextAlignment.Center));

        doc.Blocks.Add(Text(visit.Doctor.Name, 12, FontWeights.SemiBold, align: TextAlignment.Center, topMargin: 8));

        var credentials = new List<string>();
        if (!string.IsNullOrWhiteSpace(visit.Doctor.Speciality)) credentials.Add(visit.Doctor.Speciality!);
        if (!string.IsNullOrWhiteSpace(visit.Doctor.RegistrationNo)) credentials.Add($"Reg. No: {visit.Doctor.RegistrationNo}");
        if (credentials.Count > 0)
            doc.Blocks.Add(Text(string.Join("  ·  ", credentials), 10, brush: Muted, align: TextAlignment.Center));

        doc.Blocks.Add(Rule());

        var head = NewTable(1, 1);
        var headGroup = new TableRowGroup();
        headGroup.Rows.Add(Row(false,
            $"{visit.Patient.Name}  ({visit.Patient.Age}{visit.Patient.Gender.ToString()[0]})",
            $"Date: {visit.ScheduledOn:dd MMM yyyy}"));
        headGroup.Rows.Add(Row(false,
            $"Patient No: {visit.Patient.PatientNo}",
            $"Token: {visit.TokenNo}   Visit: {visit.VisitNo}"));
        head.RowGroups.Add(headGroup);
        doc.Blocks.Add(head);

        var vitals = new List<string>();
        if (visit.WeightKg is { } w) vitals.Add($"Wt {w:0.#} kg");
        if (!string.IsNullOrWhiteSpace(visit.BloodPressure)) vitals.Add($"BP {visit.BloodPressure}");
        if (visit.TemperatureF is { } t) vitals.Add($"Temp {t:0.#} °F");
        if (vitals.Count > 0)
            doc.Blocks.Add(Text(string.Join("   ·   ", vitals), 10, brush: Muted));

        if (!string.IsNullOrWhiteSpace(visit.Complaint))
            doc.Blocks.Add(Text($"Complaint: {visit.Complaint}", 10.5, topMargin: 4));

        if (!string.IsNullOrWhiteSpace(visit.Diagnosis))
            doc.Blocks.Add(Text($"Diagnosis: {visit.Diagnosis}", 10.5, FontWeights.SemiBold));

        doc.Blocks.Add(Text("Rx", 22, FontWeights.Bold, topMargin: 10, bottomMargin: 2));

        if (visit.Prescription.Count > 0)
        {
            var table = NewTable(3.4, 1.4, 1.2, 0.8, 0.8);
            var group = new TableRowGroup();
            group.Rows.Add(Row(true, "MEDICINE", "DOSE", "FREQUENCY", "DAYS", "QTY"));

            foreach (var item in visit.Prescription)
            {
                group.Rows.Add(Row(false,
                    item.MedicineName,
                    item.Dosage ?? "",
                    item.Frequency ?? "",
                    item.Days > 0 ? item.Days.ToString() : "",
                    item.Quantity > 0 ? item.Quantity.ToString() : ""));

                if (!string.IsNullOrWhiteSpace(item.Instructions))
                    group.Rows.Add(Row(false, $"    {item.Instructions}", "", "", "", ""));
            }

            table.RowGroups.Add(group);
            doc.Blocks.Add(table);
        }
        else
        {
            doc.Blocks.Add(Text("No medicines prescribed.", 10.5, brush: Muted));
        }

        if (!string.IsNullOrWhiteSpace(visit.Notes))
            doc.Blocks.Add(Text($"Advice: {visit.Notes}", 10.5, topMargin: 6));

        if (visit.FollowUpOn is { } follow)
            doc.Blocks.Add(Text($"Review on {follow:dd MMM yyyy}", 11, FontWeights.SemiBold, topMargin: 6));

        doc.Blocks.Add(Text(visit.Doctor.Name, 10.5, FontWeights.SemiBold,
                            align: TextAlignment.Right, topMargin: 34));
        doc.Blocks.Add(Text("Signature", 9.5, brush: Muted, align: TextAlignment.Right));

        return doc;
    }
}
