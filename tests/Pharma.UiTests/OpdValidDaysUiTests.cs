using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace Pharma.UiTests;

/// <summary>
/// The OPD validity window through the real screens: set the days on a doctor
/// in Settings, book and pay a first visit, then book the same patient with the
/// same doctor again and watch the desk be told it is a free review.
///
/// The arithmetic itself is pinned in <c>OpdValidDaysTests</c>. What this adds
/// is the half that only exists on screen — that the fee drops on its own, and
/// that the desk is told why rather than being left to wonder.
/// </summary>
public class OpdValidDaysUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    /// <summary>Adds a doctor with a validity window and returns their name.</summary>
    private string DoctorWithWindow(string suffix, int days)
    {
        var name = $"Dr Window {suffix}";

        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Doctors");
        app.Click("NewDoctor");

        app.Type("DoctorName", name);
        app.Type("DoctorSpeciality", "Paediatrics");
        app.Type("DoctorRegistrationNo", $"REG-{suffix}");
        app.Type("DoctorQualification", "MBBS, MD (Paediatrics)");
        app.Type("ConsultationFee", "300");
        app.Type("DoctorOpdValidDays", days.ToString());
        app.Click("DoctorSave");

        AppFixture.WaitUntil(
            () => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "the doctor to save");

        return name;
    }

    private void ClosePreview()
    {
        var preview = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;

        preview?.FindFirstDescendant(cf => cf.ByAutomationId("PreviewClose"))?.AsButton().Invoke();

        AppFixture.WaitUntil(
            () => app.MainWindow.ModalWindows.All(
                w => !w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase)),
            "the receipt preview to close");
    }

    private void OpenBookingForm()
    {
        app.Navigate("NavOpd", "OPD");
        app.Click("OpdNewVisit");
        AppFixture.WaitUntil(() => app.Find("OpdPatientSearch") is not null, "the booking form");
    }

    /// <summary>
    /// Registers a new patient against the named doctor. BookWalkIn cannot be
    /// used here: it leaves the doctor at whatever the form defaulted to, and
    /// this whole feature hangs on which doctor was seen.
    /// </summary>
    private void BookNewPatient(string patient, string phone, string age, string doctor)
    {
        OpenBookingForm();

        app.Type("OpdPatientSearch", patient);
        app.Click("OpdFind");
        AppFixture.WaitUntil(() => app.Find("OpdNewName") is not null, "the new-patient form");

        app.Type("OpdNewName", patient);
        app.Type("OpdNewPhone", phone);
        app.Type("OpdNewAge", age);

        app.ComboBox("OpdDoctor").Select(doctor);
        app.Click("OpdBook");

        AppFixture.WaitUntil(
            () => app.TextOf("OpdStatus").Contains("booked", StringComparison.OrdinalIgnoreCase),
            "the booking confirmation");
    }

    /// <summary>Opens the form for an existing patient and picks the doctor,
    /// leaving it open so the caller can read what it decided.</summary>
    private void OpenBookingFor(string patient, string doctor)
    {
        OpenBookingForm();

        app.Type("OpdPatientSearch", patient);
        app.Click("OpdFind");

        AppFixture.WaitUntil(() => app.ListBox("OpdMatches").Items.Length >= 1, "the patient to be found");
        app.ListBox("OpdMatches").Items[0].Select();

        app.ComboBox("OpdDoctor").Select(doctor);
    }

    private static decimal FeeOn(AppFixture app)
        => decimal.TryParse(app.TextBox("OpdFee").Text, out var v) ? v : -1m;

    [Fact]
    public void A_second_visit_inside_the_window_is_offered_free_and_says_why()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var doctor = DoctorWithWindow(suffix, days: 7);
        var patient = $"Window Patient {suffix}";

        // First visit: charged at the doctor's own fee, and paid.
        BookNewPatient(patient, "9876500201", "8", doctor);
        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", patient), "the first tile");
        app.TakeFee("OpdWaitingList", patient);
        ClosePreview();

        // Second visit, same doctor, same day — inside a seven-day window.
        OpenBookingFor(patient, doctor);

        AppFixture.WaitUntil(() => app.Find("OpdFeeCoverNote") is not null,
                             "the review banner to appear on the booking form");

        var note = app.TextOf("OpdFeeCoverNote");
        Assert.Contains("Review", note);
        Assert.Contains("free with this doctor until", note);

        // The fee dropped to nil on its own.
        AppFixture.WaitUntil(() => FeeOn(app) == 0m, "the fee to fall to zero");

        app.Click("OpdCloseBooking");
    }

    [Fact]
    public void A_doctor_with_no_window_never_offers_a_free_review()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");

        // Zero days — the default, and what every doctor is until somebody
        // deliberately turns the scheme on.
        var doctor = DoctorWithWindow(suffix, days: 0);
        var patient = $"No Window {suffix}";

        BookNewPatient(patient, "9876500202", "30", doctor);
        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", patient), "the first tile");
        app.TakeFee("OpdWaitingList", patient);
        ClosePreview();

        OpenBookingFor(patient, doctor);

        // No banner, and the fee is the doctor's own rather than nil.
        Assert.Null(app.Find("OpdFeeCoverNote"));
        Assert.Equal(300m, FeeOn(app));

        app.Click("OpdCloseBooking");
    }
}
