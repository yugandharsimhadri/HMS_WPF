namespace Pharma.UiTests;

/// <summary>Drives the OPD desk exactly as a receptionist would.</summary>
public class OpdUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    /// <summary>Open booking, search for someone new, fill the form, book.</summary>
    internal static void BookWalkIn(AppFixture app, string name, string phone, string age)
    {
        app.Navigate("NavOpd", "OPD");
        app.Click("OpdNewVisit");

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
    public void Booking_a_walk_in_puts_a_tile_in_the_waiting_column()
    {
        app.Navigate("NavOpd", "OPD");
        var before = app.TileCount("OpdWaitingList");

        var name = $"UI Walkin {DateTime.Now:HHmmssfff}";
        BookWalkIn(app, name, "9876500011", "34");

        AppFixture.WaitUntil(() => app.TileCount("OpdWaitingList") == before + 1, "the waiting column to grow");

        Assert.True(app.HasTile("OpdWaitingList", name));
        Assert.Equal(before + 1, app.TileCount("OpdWaitingList"));
    }

    [Fact]
    public void Marking_a_visit_done_moves_the_tile_to_completed()
    {
        var name = $"UI Done {DateTime.Now:HHmmssfff}";
        BookWalkIn(app, name, "9876500077", "6");

        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", name), "the tile to appear in waiting");
        Assert.False(app.HasTile("OpdCompletedList", name));

        app.ClickTile("OpdWaitingList", "TileDone", name);

        AppFixture.WaitUntil(() => app.HasTile("OpdCompletedList", name), "the tile to move to completed");

        Assert.True(app.HasTile("OpdCompletedList", name));
        Assert.False(app.HasTile("OpdWaitingList", name));
    }

    [Fact]
    public void Reopening_a_completed_visit_moves_the_tile_back()
    {
        var name = $"UI Reopen {DateTime.Now:HHmmssfff}";
        BookWalkIn(app, name, "9876500088", "9");

        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", name), "the tile to appear in waiting");
        app.ClickTile("OpdWaitingList", "TileDone", name);
        AppFixture.WaitUntil(() => app.HasTile("OpdCompletedList", name), "the tile to move to completed");

        app.ClickTile("OpdCompletedList", "TileReopen", name);

        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", name), "the tile to move back to waiting");
        Assert.False(app.HasTile("OpdCompletedList", name));
    }

    [Fact]
    public void A_booked_patient_reaches_the_patient_register_with_a_number()
    {
        var name = $"UI Register {DateTime.Now:HHmmssfff}";
        BookWalkIn(app, name, "9876500022", "41");

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
        BookWalkIn(app, name, "9876500033", "28");

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
        BookWalkIn(app, name, "9876500044", "52");

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
