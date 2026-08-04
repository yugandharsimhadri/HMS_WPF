namespace Pharma.Automation.Support;

/// <summary>
/// Books a walk-in OPD visit — the precondition every consultation, prescription
/// and fee-collection workflow needs a patient tile to act on. Mirrors the
/// sequence OpdUiTests.BookWalkIn already proves correct; kept here rather than
/// referenced from there since Pharma.Automation cannot depend on the test
/// project.
/// </summary>
internal static class OpdVisitSupport
{
    public static void BookWalkIn(AppFixture app, string name, string phone, string age)
    {
        app.Navigate("NavOpd", "OPD");
        app.Click("OpdNewVisit");

        AppFixture.WaitUntil(() => app.Find("OpdPatientSearch") is not null, "the booking form");

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

        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", name), "the tile to appear in waiting");
    }
}
