namespace Pharma.UiTests;

/// <summary>
/// Capturing an age as years and months through the patient form.
///
/// The arithmetic is pinned in <c>PatientAgeTests</c>. What matters here is
/// that the months box is genuinely optional — a clinic registering adults all
/// day must never be made to type a zero — and that a baby can be registered
/// in months alone, with no years at all.
/// </summary>
public class PatientAgeMonthsUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private void OpenNewPatientForm()
    {
        app.Navigate("NavPatients", "Patients");
        app.Click("PatientsNew");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the patient form");
    }

    private void SaveAndWait()
    {
        app.Click("PatientSave");
        AppFixture.WaitUntil(() => app.TextOf("PatientsStatus").Contains("saved"), "the patient to save");
        AppFixture.WaitUntil(() => app.Find("PatientName") is null, "the form to close");
    }

    private string FindAgeOf(string name)
    {
        app.Type("PatientsSearchBox", name);
        app.Click("PatientsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == 1, "the patient to be found");

        return app.Grid("PatientsGrid").Rows[0].Cells[3].Value ?? "";
    }

    /// <summary>
    /// The point of the whole change: a baby can be recorded in months, and
    /// the register shows the months rather than flattening them to "0".
    /// </summary>
    [Fact]
    public void A_baby_can_be_registered_in_months_and_reads_back_in_months()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"Baby Months {suffix}";

        OpenNewPatientForm();
        app.Type("PatientName", name);
        app.Type("PatientPhone", $"9{suffix}");
        app.Type("PatientAge", "0");
        app.Type("PatientAgeMonths", "7");
        SaveAndWait();

        Assert.Equal("7m", FindAgeOf(name));
    }

    /// <summary>
    /// Months left blank must save, because most of a general clinic's list is
    /// adults and nobody should have to type a zero to get past the field.
    /// </summary>
    [Fact]
    public void Months_may_be_left_blank_entirely()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"No Months {suffix}";

        OpenNewPatientForm();
        app.Type("PatientName", name);
        app.Type("PatientPhone", $"9{suffix}");
        app.Type("PatientAge", "41");
        // PatientAgeMonths deliberately untouched.
        SaveAndWait();

        // And it reads back without a stray "0m" hanging off it.
        Assert.Equal("41y", FindAgeOf(name));
    }

    [Fact]
    public void Years_and_months_together_read_as_both()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"Both Parts {suffix}";

        OpenNewPatientForm();
        app.Type("PatientName", name);
        app.Type("PatientPhone", $"9{suffix}");
        app.Type("PatientAge", "4");
        app.Type("PatientAgeMonths", "3");
        SaveAndWait();

        Assert.Equal("4y 3m", FindAgeOf(name));
    }

    /// <summary>
    /// Years blank and months filled is a complete answer for an infant, so it
    /// must not be turned away for want of a year.
    /// </summary>
    [Fact]
    public void Months_alone_is_a_complete_age()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"Months Only {suffix}";

        OpenNewPatientForm();
        app.Type("PatientName", name);
        app.Type("PatientPhone", $"9{suffix}");
        app.Type("PatientAgeMonths", "5");
        SaveAndWait();

        Assert.Equal("5m", FindAgeOf(name));
    }
}
