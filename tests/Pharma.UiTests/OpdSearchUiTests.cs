namespace Pharma.UiTests;

/// <summary>
/// Finding a patient at the OPD desk, by name and by phone.
///
/// A phone number is matched on its digits alone, so the spacing and the +91 a
/// receptionist types — or does not — make no difference. A family shares one
/// number, so it returns all of them rather than the first.
/// </summary>
public class OpdSearchUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private void OpenBooking()
    {
        app.Navigate("NavOpd", "OPD");
        app.Click("OpdNewVisit");
    }

    private void Search(string term)
    {
        OpenBooking();
        app.Type("OpdPatientSearch", term);
        app.Click("OpdFind");
    }

    /// <summary>
    /// Two children on one number — and a number of its own per test, because
    /// these all share one running application and one database, so a shared
    /// number would leave each test finding the families the others booked.
    /// </summary>
    private (string Suffix, string Phone) GivenAFamily()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var phone = $"9{suffix}";

        OpdUiTests.BookWalkIn(app, $"Aarav {suffix}", phone, "4");
        OpdUiTests.BookWalkIn(app, $"Diya {suffix}", phone, "7");

        return (suffix, phone);
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
        var (suffix, _) = GivenAFamily();

        Search($"Aarav {suffix}");

        AppFixture.WaitUntil(() => app.ListBox("OpdMatches").Items.Length == 1, "the patient by name");
        Assert.Contains("Aarav", app.ListBox("OpdMatches").Items[0].Text);

        app.Click("OpdCloseBooking");
    }

    [Fact]
    public void A_phone_number_finds_everyone_on_it()
    {
        var (_, phone) = GivenAFamily();

        Search(phone);

        // Both children, because a family shares the number and the desk has to
        // pick which one is actually here.
        AppFixture.WaitUntil(() => app.ListBox("OpdMatches").Items.Length == 2, "the whole family");

        var names = app.ListBox("OpdMatches").Items.Select(i => i.Text ?? "").ToList();

        Assert.Contains(names, n => n.Contains("Aarav"));
        Assert.Contains(names, n => n.Contains("Diya"));

        Assert.Contains("registered on this number", app.TextOf("OpdStatus"));

        app.Click("OpdCloseBooking");
    }

    [Theory]
    [InlineData("spaced")]      // 98765 12345, as it is read out
    [InlineData("country")]     // +91 9876512345
    [InlineData("dashed")]      // 98765-12345
    public void A_phone_number_is_found_however_it_is_typed(string style)
    {
        var (_, phone) = GivenAFamily();
        var typed = Format(phone, style);

        Search(typed);

        AppFixture.WaitUntil(() => app.ListBox("OpdMatches").Items.Length == 2,
                             $"the family from '{typed}'");

        app.Click("OpdCloseBooking");
    }

    [Fact]
    public void Part_of_a_phone_number_still_narrows_it_down()
    {
        var (_, phone) = GivenAFamily();

        Search(phone[4..]);

        AppFixture.WaitUntil(() => app.ListBox("OpdMatches").Items.Length >= 2, "the family from the last digits");

        app.Click("OpdCloseBooking");
    }

    [Fact]
    public void Nobody_matching_offers_to_add_them()
    {
        Search($"Nobody {DateTime.Now:HHmmssfff}");

        AppFixture.WaitUntil(() => app.Find("OpdNewName") is not null, "the new-patient form");
        Assert.Contains("No one matches", app.TextOf("OpdStatus"));

        app.Click("OpdCloseBooking");
    }
}
