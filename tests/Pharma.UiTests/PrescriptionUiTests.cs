using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace Pharma.UiTests;

/// <summary>
/// Choosing a medicine on a prescription. Searching our pharmacy has to work,
/// and a medicine we do not stock has to be prescribable anyway.
/// </summary>
public class PrescriptionUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private Window OpenConsultationFor(string patient)
    {
        OpdUiTests.BookWalkIn(app, patient, "9001002003", "5");

        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", patient), "the tile to appear");
        app.ClickTile("OpdWaitingList", "TileConsult", patient);

        var window = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.Contains("Consultation", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;

        Assert.NotNull(window);

        AppFixture.WaitUntil(
            () => (window!.FindFirstDescendant(cf => cf.ByAutomationId("ConsultationHeader"))
                          ?.AsLabel().Text ?? "").Contains(patient),
            "the consultation to load");

        return window!;
    }

    private static void Type(Window window, string automationId, string text)
    {
        var box = window.FindFirstDescendant(cf => cf.ByAutomationId(automationId))!.AsTextBox();
        box.Focus();
        box.Text = text;
    }

    private static string TextOf(Window window, string automationId)
        => window.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsLabel().Text ?? "";

    /// <summary>Picks a morning, afternoon or night dose from its list.</summary>
    private static void Dose(Window window, string automationId, string value)
        => window.FindFirstDescendant(cf => cf.ByAutomationId(automationId))!.AsComboBox().Select(value);

    private static AutomationElement[] Matches(Window window)
        => window.FindFirstDescendant(cf => cf.ByAutomationId("RxMatches"))
                 ?.FindAllDescendants(cf => cf.ByAutomationId("RxMatch")) ?? [];

    [Fact]
    public void Typing_part_of_a_name_searches_our_pharmacy()
    {
        var window = OpenConsultationFor($"Rx Search {DateTime.Now:HHmmssfff}");

        // "Paracetamol 500mg" is seeded on first run.
        Type(window, "RxMedicine", "Parac");

        AppFixture.WaitUntil(() => Matches(window).Length > 0, "the medicine search results");

        var names = Matches(window).Select(m => m.Name ?? "").ToList();
        Assert.Contains(names, n => n.Contains("Paracetamol", StringComparison.OrdinalIgnoreCase));

        window.Close();
    }

    [Fact]
    public void Choosing_a_result_links_the_line_to_our_stock()
    {
        var window = OpenConsultationFor($"Rx Pick {DateTime.Now:HHmmssfff}");

        Type(window, "RxMedicine", "Cetiriz");
        AppFixture.WaitUntil(() => Matches(window).Length > 0, "the medicine search results");

        Matches(window)[0].AsButton().Invoke();

        AppFixture.WaitUntil(
            () => TextOf(window, "RxMedicineHint").Contains("In our pharmacy"),
            "the linked-to-stock note");

        Assert.Contains("In our pharmacy", TextOf(window, "RxMedicineHint"));

        // Choosing one closes the list.
        Assert.Empty(Matches(window));

        window.Close();
    }

    [Fact]
    public void A_medicine_we_do_not_stock_can_still_be_prescribed()
    {
        var window = OpenConsultationFor($"Rx Outside {DateTime.Now:HHmmssfff}");

        Type(window, "RxMedicine", "Imported Ointment 20g");

        AppFixture.WaitUntil(
            () => TextOf(window, "RxMedicineHint").Contains("Not in our pharmacy"),
            "the not-stocked note");

        Assert.Contains("prescription only", TextOf(window, "RxMedicineHint"));

        // Nothing is pre-filled, so the course has to be stated.
        Dose(window, "RxMorning", "1");
        Dose(window, "RxNight", "1");
        Type(window, "RxDays", "3");

        AppFixture.WaitUntil(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("RxQuantity"))!.AsTextBox().Text == "6",
            "the course to be worked out");

        window.FindFirstDescendant(cf => cf.ByAutomationId("RxAdd"))!.AsButton().Invoke();

        AppFixture.WaitUntil(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("RxGrid"))!.AsGrid().RowCount == 1,
            "the line to be added");

        var cells = window.FindFirstDescendant(cf => cf.ByAutomationId("RxGrid"))!
                          .AsGrid().Rows[0].Cells.Select(c => c.Value ?? "").ToArray();

        Assert.Equal("Imported Ointment 20g", cells[0]);

        window.Close();
    }

    [Fact]
    public void Nothing_is_filled_in_before_the_doctor_types_it()
    {
        var window = OpenConsultationFor($"Rx Blank {DateTime.Now:HHmmssfff}");

        // A pre-filled dose is a clinical decision the software should not make.
        Assert.Equal("", window.FindFirstDescendant(cf => cf.ByAutomationId("RxMedicine"))!.AsTextBox().Text);
        Assert.Equal("", window.FindFirstDescendant(cf => cf.ByAutomationId("RxDose"))!.AsTextBox().Text);
        Assert.Equal("0", window.FindFirstDescendant(cf => cf.ByAutomationId("RxMorning"))!
                                .AsComboBox().SelectedItems[0].Text);
        Assert.Equal("0", window.FindFirstDescendant(cf => cf.ByAutomationId("RxDays"))!.AsTextBox().Text);
        Assert.Equal("0", window.FindFirstDescendant(cf => cf.ByAutomationId("RxQuantity"))!.AsTextBox().Text);
        Assert.Equal("", TextOf(window, "RxCourseHint"));

        window.Close();
    }

    [Fact]
    public void The_course_is_worked_out_in_individual_units()
    {
        var window = OpenConsultationFor($"Rx Course {DateTime.Now:HHmmssfff}");

        // One morning, one afternoon, one night.
        Dose(window, "RxMorning", "1");
        Dose(window, "RxAfternoon", "1");
        Dose(window, "RxNight", "1");
        Type(window, "RxDays", "5");

        AppFixture.WaitUntil(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("RxQuantity"))!.AsTextBox().Text == "15",
            "the course to be worked out");

        // Three a day for five days is fifteen tablets, not fifteen strips.
        Assert.Equal("15", window.FindFirstDescendant(cf => cf.ByAutomationId("RxQuantity"))!.AsTextBox().Text);
        Assert.Contains("15 units", TextOf(window, "RxCourseHint"));

        window.Close();
    }

    [Fact]
    public void A_half_dose_morning_and_night_is_understood()
    {
        var window = OpenConsultationFor($"Rx Half {DateTime.Now:HHmmssfff}");

        // Half a tablet twice a day for six days is six tablets.
        Dose(window, "RxMorning", "1/2");
        Dose(window, "RxNight", "1/2");
        Type(window, "RxDays", "6");

        AppFixture.WaitUntil(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("RxQuantity"))!.AsTextBox().Text == "6",
            "the half-dose course to be worked out");

        Assert.Equal("6", window.FindFirstDescendant(cf => cf.ByAutomationId("RxQuantity"))!.AsTextBox().Text);

        window.Close();
    }
}
