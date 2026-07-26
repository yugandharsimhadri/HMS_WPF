namespace Pharma.UiTests;

/// <summary>Drives the OPD desk exactly as a receptionist would.</summary>
[Collection("ui")]
public class OpdUiTests(AppFixture app)
{
    /// <summary>
    /// The three booking steps: search for a patient who does not exist, fill the
    /// new-patient form the search offers, book.
    /// </summary>
    private void BookWalkIn(string name, string phone, string age)
    {
        app.Navigate("NavOpd", "OPD");

        app.Type("OpdPatientSearch", name);
        app.Click("OpdFind");

        AppFixture.WaitUntil(() => app.Find("OpdNewName") is not null, "the new-patient form");

        app.Type("OpdNewName", name);
        app.Type("OpdNewPhone", phone);
        app.Type("OpdNewAge", age);

        app.Click("OpdBook");

        AppFixture.WaitUntil(
            () => app.TextOf("OpdStatus").Contains("booked", StringComparison.OrdinalIgnoreCase),
            "the booking confirmation");
    }

    [Fact]
    public void Booking_a_walk_in_adds_them_to_todays_queue()
    {
        app.Navigate("NavOpd", "OPD");
        var before = app.Grid("OpdQueueGrid").RowCount;

        BookWalkIn($"UI Walkin {DateTime.Now:HHmmssfff}", "9876500011", "34");

        Assert.Contains("booked", app.TextOf("OpdStatus"), StringComparison.OrdinalIgnoreCase);

        AppFixture.WaitUntil(() => app.Grid("OpdQueueGrid").RowCount == before + 1, "the queue to grow");
        Assert.Equal(before + 1, app.Grid("OpdQueueGrid").RowCount);
    }

    [Fact]
    public void A_booked_patient_reaches_the_patient_register_with_a_number()
    {
        var name = $"UI Register {DateTime.Now:HHmmssfff}";
        BookWalkIn(name, "9876500022", "41");

        app.Navigate("NavPatients", "Patients");
        app.Type("PatientsSearchBox", name);
        app.Click("PatientsSearchButton");

        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == 1, "the patient to be found");

        var cells = app.Grid("PatientsGrid").Rows[0].Cells.Select(c => c.Value ?? "").ToArray();

        Assert.StartsWith("P", cells[0]);            // a patient number was allocated
        Assert.Equal(name, cells[1]);
        Assert.Equal("9876500022", cells[2]);
        Assert.Equal("41", cells[3]);
    }

    [Fact]
    public void A_booking_appears_in_the_patients_visit_history()
    {
        var name = $"UI History {DateTime.Now:HHmmssfff}";
        BookWalkIn(name, "9876500033", "28");

        app.Navigate("NavPatients", "Patients");
        app.Type("PatientsSearchBox", name);
        app.Click("PatientsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == 1, "the patient to be found");

        app.Grid("PatientsGrid").Rows[0].Select();

        AppFixture.WaitUntil(() => app.Grid("PatientHistoryGrid").RowCount == 1, "the visit history");
        Assert.Equal(1, app.Grid("PatientHistoryGrid").RowCount);
    }

    [Fact]
    public void Editing_a_patient_saves_the_change()
    {
        var name = $"UI Edit {DateTime.Now:HHmmssfff}";
        BookWalkIn(name, "9876500044", "52");

        app.Navigate("NavPatients", "Patients");
        app.Type("PatientsSearchBox", name);
        app.Click("PatientsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == 1, "the patient to be found");

        app.Grid("PatientsGrid").Rows[0].Select();
        AppFixture.WaitUntil(() => app.TextBox("PatientName").Text == name, "the editor to fill");

        app.Type("PatientAllergies", "Penicillin");
        app.Click("PatientSave");

        AppFixture.WaitUntil(
            () => app.TextOf("PatientsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "the save confirmation");

        // Re-read from the database by searching again.
        app.Type("PatientsSearchBox", name);
        app.Click("PatientsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == 1, "the patient to be found");

        var cells = app.Grid("PatientsGrid").Rows[0].Cells.Select(c => c.Value ?? "").ToArray();
        Assert.Equal("Penicillin", cells[5]);
    }
}
