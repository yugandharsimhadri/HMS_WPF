namespace Pharma.UiTests;

/// <summary>
/// A paediatric clinic registers several children against one parent's phone.
/// Searching that number must offer all of them, and booking must never quietly
/// create a fourth child because nobody was picked.
/// </summary>
public class FamilyPhoneUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private const string FamilyPhone = "9008007001";

    private string[] RegisterFamily()
    {
        var stamp = DateTime.Now.ToString("HHmmssfff");
        string[] names = [$"Sibling A {stamp}", $"Sibling B {stamp}", $"Sibling C {stamp}"];

        OpdUiTests.BookWalkIn(app, names[0], FamilyPhone, "3");
        OpdUiTests.BookWalkIn(app, names[1], FamilyPhone, "6");
        OpdUiTests.BookWalkIn(app, names[2], FamilyPhone, "9");

        return names;
    }

    private void SearchFor(string term)
    {
        app.Navigate("NavOpd", "OPD");
        app.Click("OpdNewVisit");
        app.Type("OpdPatientSearch", term);
        app.Click("OpdFind");
    }

    [Fact]
    public void Searching_one_phone_number_lists_every_child_on_it()
    {
        var names = RegisterFamily();

        SearchFor(FamilyPhone);
        AppFixture.WaitUntil(() => app.ListBox("OpdMatches").Items.Length >= 3, "the family to be listed");

        var listed = app.ListBox("OpdMatches").Items.Select(i => i.Text ?? "").ToList();

        foreach (var name in names)
            Assert.Contains(listed, entry => entry.Contains(name));
    }

    [Fact]
    public void A_shared_number_tells_the_operator_to_pick_one()
    {
        RegisterFamily();

        SearchFor(FamilyPhone);

        AppFixture.WaitUntil(
            () => app.TextOf("OpdStatus").Contains("registered on this number", StringComparison.OrdinalIgnoreCase),
            "the shared-number notice");

        Assert.Contains("Select which one is here", app.TextOf("OpdStatus"));
    }

    [Fact]
    public void A_formatted_number_still_finds_the_family()
    {
        var names = RegisterFamily();

        // Stored as 9008007001; typed the way a parent reads it out.
        SearchFor("+91 90080 07001");
        AppFixture.WaitUntil(() => app.ListBox("OpdMatches").Items.Length >= 3, "the family to be listed");

        var listed = app.ListBox("OpdMatches").Items.Select(i => i.Text ?? "").ToList();
        Assert.Contains(listed, entry => entry.Contains(names[0]));
    }

    [Fact]
    public void Booking_without_choosing_a_sibling_is_refused_rather_than_duplicating()
    {
        RegisterFamily();

        app.Navigate("NavOpd", "OPD");
        var before = app.TileCount("OpdWaitingList");

        app.Click("OpdNewVisit");
        app.Type("OpdPatientSearch", FamilyPhone);
        app.Click("OpdFind");
        AppFixture.WaitUntil(() => app.ListBox("OpdMatches").Items.Length >= 3, "the family to be listed");

        // Book with nobody selected. A modal warning appears; dismiss it.
        app.Click("OpdBook");

        var warning = FlaUI.Core.Tools.Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(),
            TimeSpan.FromSeconds(10)).Result;

        Assert.NotNull(warning);
        Assert.Contains("Select which one", string.Join(" ", warning!.FindAllDescendants().Select(d => d.Name ?? "")));

        app.DismissModals();

        // Nothing was booked and no fourth child was invented.
        app.Navigate("NavOpd", "OPD");
        Assert.Equal(before, app.TileCount("OpdWaitingList"));
    }
}
