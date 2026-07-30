namespace Pharma.UiTests;

/// <summary>
/// Finding a patient while booking, by name and by phone.
///
/// A phone number is matched on its digits alone, so the spacing and the +91 a
/// receptionist types — or does not — make no difference. A paediatric clinic
/// registers several children against one parent's number, so a search returns
/// all of them rather than the first, and booking must never quietly invent a
/// fourth child because nobody was picked.
/// </summary>
public class OpdSearchUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private void OpenBooking()
    {
        app.Navigate("NavOpd", "OPD");
        app.Click("OpdNewVisit");
        AppFixture.WaitUntil(() => app.Find("OpdPatientSearch") is not null, "the booking form");
    }

    private void Search(string term)
    {
        OpenBooking();
        app.Type("OpdPatientSearch", term);
        app.Click("OpdFind");
    }

    /// <summary>
    /// Three children on one number — and a number of its own per test, because
    /// these all share one running application and one database, so a shared
    /// number would leave each test finding the families the others booked.
    /// </summary>
    private (string Suffix, string Phone, string[] Names) GivenAFamily()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var phone = $"9{suffix}";

        string[] names = [$"Aarav {suffix}", $"Diya {suffix}", $"Kabir {suffix}"];

        foreach (var (name, age) in names.Zip(new[] { "4", "7", "9" }))
            OpdUiTests.BookWalkIn(app, name, phone, age);

        return (suffix, phone, names);
    }

    /// <summary>The same number as a receptionist might type it.</summary>
    private static string Format(string phone, string style) => style switch
    {
        "spaced" => $"{phone[..5]} {phone[5..]}",
        "country" => $"+91 {phone}",
        "dashed" => $"{phone[..5]}-{phone[5..]}",
        _ => phone
    };

    [Fact]
    public void A_patient_is_found_by_name()
    {
        var (suffix, _, _) = GivenAFamily();

        Search($"Aarav {suffix}");

        AppFixture.WaitUntil(() => app.ListBox("OpdMatches").Items.Length == 1, "the patient by name");
        Assert.Contains("Aarav", app.ListBox("OpdMatches").Items[0].Text);

        app.Click("OpdCloseBooking");
    }

    [Fact]
    public void A_phone_number_finds_everyone_on_it_and_says_to_pick_one()
    {
        var (_, phone, names) = GivenAFamily();

        Search(phone);

        // All three children, because a family shares the number and the desk
        // has to pick which one is actually here.
        AppFixture.WaitUntil(() => app.ListBox("OpdMatches").Items.Length == 3, "the whole family");

        var listed = app.ListBox("OpdMatches").Items.Select(i => i.Text ?? "").ToList();
        foreach (var name in names) Assert.Contains(listed, entry => entry.Contains(name));

        // And it says so, rather than leaving the operator to notice.
        var said = app.TextOf("BookVisitStatus");
        Assert.Contains("registered on this number", said);
        Assert.Contains("Select which one is here", said);

        app.Click("OpdCloseBooking");
    }

    [Theory]
    [InlineData("spaced")]      // 98765 12345, as it is read out
    [InlineData("country")]     // +91 9876512345
    [InlineData("dashed")]      // 98765-12345
    public void A_phone_number_is_found_however_it_is_typed(string style)
    {
        var (_, phone, _) = GivenAFamily();
        var typed = Format(phone, style);

        Search(typed);

        AppFixture.WaitUntil(() => app.ListBox("OpdMatches").Items.Length == 3,
                             $"the family from '{typed}'");

        app.Click("OpdCloseBooking");
    }

    [Fact]
    public void Part_of_a_phone_number_still_narrows_it_down()
    {
        var (_, phone, _) = GivenAFamily();

        Search(phone[4..]);

        AppFixture.WaitUntil(() => app.ListBox("OpdMatches").Items.Length >= 3, "the family from the last digits");

        app.Click("OpdCloseBooking");
    }

    [Fact]
    public void Nobody_matching_offers_to_add_them()
    {
        Search($"Nobody {DateTime.Now:HHmmssfff}");

        AppFixture.WaitUntil(() => app.Find("OpdNewName") is not null, "the new-patient form");
        Assert.Contains("No one matches", app.TextOf("BookVisitStatus"));

        app.Click("OpdCloseBooking");
    }

    /// <summary>
    /// The one that matters most: siblings share a number, and booking with
    /// nobody selected must refuse rather than register a child who is already
    /// on the books a second time.
    /// </summary>
    [Fact]
    public void Booking_without_choosing_a_sibling_is_refused_rather_than_duplicating()
    {
        var (_, phone, _) = GivenAFamily();

        app.Navigate("NavOpd", "OPD");
        var before = app.TileCount("OpdWaitingList");

        Search(phone);
        AppFixture.WaitUntil(() => app.ListBox("OpdMatches").Items.Length == 3, "the family to be listed");

        // Book with nobody selected. A modal warning appears; dismiss it.
        app.Click("OpdBook");

        var warning = FlaUI.Core.Tools.Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(),
            TimeSpan.FromSeconds(10)).Result;

        Assert.NotNull(warning);
        Assert.Contains("Select which one", string.Join(" ", warning!.FindAllDescendants().Select(d => d.Name ?? "")));

        app.DismissModals();
        app.Click("OpdCloseBooking");

        // Nothing was booked and no fourth child was invented.
        app.Navigate("NavOpd", "OPD");
        Assert.Equal(before, app.TileCount("OpdWaitingList"));
    }
}
