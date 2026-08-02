using FlaUI.Core.AutomationElements;

namespace Pharma.UiTests;

/// <summary>
/// Choosing a medicine on a prescription. Searching our pharmacy has to work,
/// and a medicine we do not stock has to be prescribable anyway.
///
/// The consultation is a layer over the shell, not a window, so everything here
/// is found in the main window.
/// </summary>
public class PrescriptionUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private void OpenConsultationFor(string patient)
    {
        OpdUiTests.BookWalkIn(app, patient, "9001002003", "5");

        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", patient), "the tile to appear");

        // One left over from the previous test would sit on top of this one.
        app.CloseConsultation();

        app.ClickTile("OpdWaitingList", "TileConsult", patient);
        app.WaitForConsultation(patient);
        app.SelectTab("ConsultationTabs", "Prescription");
    }

    private AutomationElement[] Matches()
        => app.Find("RxMatches")?.FindAllDescendants(cf => cf.ByAutomationId("RxMatch")) ?? [];

    [Fact]
    public void Typing_part_of_a_name_searches_our_pharmacy()
    {
        OpenConsultationFor($"Rx Search {DateTime.Now:HHmmssfff}");

        // "Paracetamol 500mg" is seeded on first run.
        app.Type("RxMedicine", "Parac");

        AppFixture.WaitUntil(() => Matches().Length > 0, "the medicine search results");

        var names = Matches().Select(m => m.Name ?? "").ToList();
        Assert.Contains(names, n => n.Contains("Paracetamol", StringComparison.OrdinalIgnoreCase));

        app.CloseConsultation();
    }

    [Fact]
    public void Choosing_a_result_links_the_line_to_our_stock()
    {
        OpenConsultationFor($"Rx Pick {DateTime.Now:HHmmssfff}");

        app.Type("RxMedicine", "Cetiriz");
        AppFixture.WaitUntil(() => Matches().Length > 0, "the medicine search results");

        Matches()[0].AsButton().Invoke();

        AppFixture.WaitUntil(
            () => app.TextOf("RxMedicineHint").Contains("In our pharmacy"),
            "the linked-to-stock note");

        Assert.Contains("In our pharmacy", app.TextOf("RxMedicineHint"));

        // Choosing one closes the list.
        Assert.Empty(Matches());

        app.CloseConsultation();
    }

    [Fact]
    public void A_medicine_we_do_not_stock_can_still_be_prescribed()
    {
        OpenConsultationFor($"Rx Outside {DateTime.Now:HHmmssfff}");

        app.Type("RxMedicine", "Imported Ointment 20g");

        AppFixture.WaitUntil(
            () => app.TextOf("RxMedicineHint").Contains("Not in our pharmacy"),
            "the not-stocked note");

        Assert.Contains("prescription only", app.TextOf("RxMedicineHint"));

        // Nothing is pre-filled, so the course has to be stated.
        app.ComboBox("RxMorning").Select("1");
        app.ComboBox("RxNight").Select("1");
        app.Type("RxDays", "3");

        AppFixture.WaitUntil(() => app.TextBox("RxQuantity").Text == "6", "the course to be worked out");

        app.Click("RxAdd");

        AppFixture.WaitUntil(() => app.Grid("RxGrid").RowCount == 1, "the line to be added");

        var cells = app.Grid("RxGrid").Rows[0].Cells.Select(c => c.Value ?? "").ToArray();

        Assert.Equal("Imported Ointment 20g", cells[0]);

        app.CloseConsultation();
    }

    [Fact]
    public void Nothing_is_filled_in_before_the_doctor_types_it()
    {
        OpenConsultationFor($"Rx Blank {DateTime.Now:HHmmssfff}");

        // A pre-filled dose is a clinical decision the software should not make.
        Assert.Equal("", app.TextBox("RxMedicine").Text);
        Assert.Equal("", app.TextBox("RxDose").Text);
        Assert.Equal("0", app.ComboBox("RxMorning").SelectedItems[0].Text);
        Assert.Equal("0", app.TextBox("RxDays").Text);
        Assert.Equal("0", app.TextBox("RxQuantity").Text);
        Assert.Equal("", app.TextOf("RxCourseHint"));

        app.CloseConsultation();
    }

    [Fact]
    public void The_course_is_worked_out_in_individual_units()
    {
        OpenConsultationFor($"Rx Course {DateTime.Now:HHmmssfff}");

        // One morning, one afternoon, one night.
        app.ComboBox("RxMorning").Select("1");
        app.ComboBox("RxAfternoon").Select("1");
        app.ComboBox("RxNight").Select("1");
        app.Type("RxDays", "5");

        AppFixture.WaitUntil(() => app.TextBox("RxQuantity").Text == "15", "the course to be worked out");

        // Three a day for five days is fifteen tablets, not fifteen strips.
        Assert.Equal("15", app.TextBox("RxQuantity").Text);
        Assert.Contains("15 units", app.TextOf("RxCourseHint"));

        app.CloseConsultation();
    }

    [Fact]
    public void A_half_dose_morning_and_night_is_understood()
    {
        OpenConsultationFor($"Rx Half {DateTime.Now:HHmmssfff}");

        // Half a tablet twice a day for six days is six tablets.
        app.ComboBox("RxMorning").Select("1/2");
        app.ComboBox("RxNight").Select("1/2");
        app.Type("RxDays", "6");

        AppFixture.WaitUntil(() => app.TextBox("RxQuantity").Text == "6",
                             "the half-dose course to be worked out");

        Assert.Equal("6", app.TextBox("RxQuantity").Text);

        app.CloseConsultation();
    }

    [Fact]
    public void The_consultation_cannot_be_left_open_behind_the_shell()
    {
        var patient = $"Rx Layer {DateTime.Now:HHmmssfff}";
        OpenConsultationFor(patient);

        // While it is open the shell behind it takes no input, so its buttons
        // are not reachable — nothing can be started and then forgotten.
        Assert.False(app.Button("NavSettings").IsEnabled);

        app.CloseConsultation();

        Assert.True(app.Button("NavSettings").IsEnabled);
        Assert.False(app.IsConsultationOpen);
    }
}
