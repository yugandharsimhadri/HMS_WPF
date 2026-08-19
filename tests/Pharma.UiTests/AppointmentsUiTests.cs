using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace Pharma.UiTests;

/// <summary>
/// The Appointments module end to end: booking, the daily list, check-in
/// (which has to actually produce an OPD visit for a General appointment,
/// not just flip a status), cancellation, rescheduling, and reminders.
/// </summary>
public class AppointmentsUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private void EnsureAppointmentsEnabled()
    {
        if (app.Find("NavAppointments") is not null) return;

        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");
        app.CheckBox("AppointmentsEnabled").IsChecked = true;
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(() => app.Find("NavAppointments") is not null, "the nav button to appear");
    }

    /// <summary>Registers a brand new patient from the Book appointment tab's
    /// own "+ New patient" overlay — the same one Diagnostics already uses —
    /// and leaves them selected.</summary>
    private string RegisterAndSelectPatient(string suffix)
    {
        var name = $"Apt Patient {suffix}";

        app.Click("AppointmentsNewPatient");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the patient editor to open");

        app.Type("PatientName", name);
        app.Type("PatientPhone", $"9{suffix}");
        app.Type("PatientAge", "28");
        app.Click("PatientSave");

        AppFixture.WaitUntil(() => app.Find("PatientName") is null, "the editor to close");
        AppFixture.WaitUntil(
            () => app.TextOf("AppointmentsSelectedPatient").Contains(name),
            "the newly registered patient to be selected");

        return name;
    }

    private string BookForTomorrow(string suffix, string time = "10:00")
    {
        var tomorrow = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");

        app.Navigate("NavAppointments", "Appointments");
        app.SelectTab("AppointmentsTabs", "Book appointment");

        var name = RegisterAndSelectPatient(suffix);

        app.Type("AppointmentsBookingDate", tomorrow);
        app.Type("AppointmentsBookingTime", time);
        app.Click("AppointmentsBook");

        AppFixture.WaitUntil(
            () => app.TextOf("AppointmentsBookingStatus").Contains("booked", StringComparison.OrdinalIgnoreCase),
            "the booking confirmation");

        return name;
    }

    /// <summary>Points the daily list at the given date through the real
    /// DatePicker — see AppFixture.SetDate.</summary>
    private void GoToDay(DateTime date)
    {
        app.SelectTab("AppointmentsTabs", "Today's appointments");
        app.SetDate("AppointmentsSelectedDate", date);
    }

    /// <summary>
    /// Every test in this class books onto a shared day ("tomorrow"), so more
    /// than one appointment is usually on the grid by the time a later test
    /// runs — picking row 0 would grab whichever one happens to sort first,
    /// not the one this test just created. Find it by patient name instead.
    /// </summary>
    private static void SelectRowFor(Grid grid, string patientName)
        => grid.Rows.First(r => r.Cells[1].Value == patientName).Select();

    [Fact]
    public void Enabling_appointments_shows_the_nav_button_immediately_and_it_persists()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");

        app.CheckBox("AppointmentsEnabled").IsChecked = false;
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(
            () => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "features to save (off)");
        AppFixture.WaitUntil(() => app.Find("NavAppointments") is null, "the nav button to disappear");

        app.CheckBox("AppointmentsEnabled").IsChecked = true;
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(() => app.Find("NavAppointments") is not null, "the nav button to reappear");

        app.Navigate("NavPatients", "Patients");
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");
        AppFixture.WaitUntil(() => app.CheckBox("AppointmentsEnabled").IsChecked == true, "the toggle to reload as on");
    }

    [Fact]
    public void Booking_an_appointment_shows_it_on_tomorrows_list()
    {
        EnsureAppointmentsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = BookForTomorrow(suffix);

        app.Navigate("NavPatients", "Patients");
        app.Navigate("NavAppointments", "Appointments");
        GoToDay(DateTime.Today.AddDays(1));

        AppFixture.WaitUntil(() => app.Grid("AppointmentsTodayGrid").RowCount >= 1, "tomorrow's appointment");
        Assert.Contains(app.Grid("AppointmentsTodayGrid").Rows.Select(r => r.Cells[1].Value), v => v == name);
    }

    [Fact]
    public void Checking_in_a_general_appointment_creates_an_OPD_visit()
    {
        EnsureAppointmentsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = BookForTomorrow(suffix, "09:30");

        GoToDay(DateTime.Today.AddDays(1));

        AppFixture.WaitUntil(() => app.Grid("AppointmentsTodayGrid").Rows.Any(r => r.Cells[1].Value == name), "the appointment to list");
        SelectRowFor(app.Grid("AppointmentsTodayGrid"), name);

        AppFixture.WaitUntil(() => app.Find("AppointmentsCheckIn") is not null, "the check-in action");
        app.Click("AppointmentsCheckIn");

        AppFixture.WaitUntil(
            () => app.TextOf("AppointmentsDailyStatus").Contains("checked in", StringComparison.OrdinalIgnoreCase),
            "the check-in confirmation");

        // The row itself now reads CheckedIn — check-in did not just vanish it.
        AppFixture.WaitUntil(
            () => app.Grid("AppointmentsTodayGrid").Rows.Any(r => r.Cells[1].Value == name && r.Cells[4].Value == "CheckedIn"),
            "the row to show CheckedIn");
    }

    [Fact]
    public void Cancelling_an_appointment_removes_it_from_the_pending_actions_and_marks_it_cancelled()
    {
        EnsureAppointmentsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = BookForTomorrow(suffix, "14:00");

        GoToDay(DateTime.Today.AddDays(1));

        AppFixture.WaitUntil(() => app.Grid("AppointmentsTodayGrid").Rows.Any(r => r.Cells[1].Value == name), "the appointment to list");
        var before = app.Grid("AppointmentsTodayGrid").RowCount;
        SelectRowFor(app.Grid("AppointmentsTodayGrid"), name);

        app.Type("AppointmentsCancelReason", "Patient called to cancel");
        app.Click("AppointmentsCancel");

        AppFixture.WaitUntil(
            () => app.TextOf("AppointmentsDailyStatus").Contains("cancelled", StringComparison.OrdinalIgnoreCase),
            "the cancellation confirmation");

        // Cancelled appointments still list for the day — the row now reads
        // Cancelled rather than disappearing outright, so the front desk can
        // still see what happened to the slot.
        Assert.Equal(before, app.Grid("AppointmentsTodayGrid").RowCount);
        Assert.Contains(app.Grid("AppointmentsTodayGrid").Rows, r => r.Cells[1].Value == name && r.Cells[4].Value == "Cancelled");
    }

    [Fact]
    public void Rescheduling_moves_the_appointment_off_the_original_day_and_onto_the_new_one()
    {
        EnsureAppointmentsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = BookForTomorrow(suffix, "15:00");

        var originalDay = DateTime.Today.AddDays(1);
        var newDay = DateTime.Today.AddDays(4);
        GoToDay(originalDay);

        AppFixture.WaitUntil(() => app.Grid("AppointmentsTodayGrid").Rows.Any(r => r.Cells[1].Value == name), "the appointment to list");
        SelectRowFor(app.Grid("AppointmentsTodayGrid"), name);

        app.Type("AppointmentsRescheduleDate", newDay.ToString("yyyy-MM-dd"));
        app.Type("AppointmentsRescheduleTime", "16:00");
        app.Click("AppointmentsReschedule");

        AppFixture.WaitUntil(
            () => app.TextOf("AppointmentsDailyStatus").Contains("moved", StringComparison.OrdinalIgnoreCase),
            "the reschedule confirmation");

        GoToDay(newDay);
        AppFixture.WaitUntil(() => app.Grid("AppointmentsTodayGrid").Rows.Any(r => r.Cells[1].Value == name), "the moved appointment on its new day");
    }

    /// <summary>
    /// A reminded appointment stays on the list rather than disappearing —
    /// the front desk needs to see what's already been done, not just what's
    /// still outstanding — and the action toggles both ways in case
    /// something gets marked by mistake.
    /// </summary>
    [Fact]
    public void An_upcoming_appointment_shows_on_the_reminders_tab_and_toggles_reminded_and_back()
    {
        EnsureAppointmentsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = BookForTomorrow(suffix, "12:00");

        // Switching to the Reminders tab reloads it — see
        // Switching_tabs_within_the_page_reloads_each_tabs_own_data for the
        // regression this guards (each tab used to only load once, at page
        // navigation, so a booking made on the Book tab never showed up here
        // without leaving the page and coming back).
        app.SelectTab("AppointmentsTabs", "Reminders");
        AppFixture.WaitUntil(() => app.Grid("AppointmentsRemindersGrid").Rows.Any(r => r.Cells[1].Value == name), "the reminder to appear");

        var before = app.Grid("AppointmentsRemindersGrid").RowCount;
        SelectRowFor(app.Grid("AppointmentsRemindersGrid"), name);

        AppFixture.WaitUntil(() => app.Button("AppointmentsMarkReminded").Name == "Mark reminded", "a not-yet-reminded row to offer marking it done");
        app.Click("AppointmentsMarkReminded");

        // Still on the list — not removed, just marked. The REMINDED badge
        // column is a DataGridTemplateColumn (needed for the pill look, not
        // a plain checkbox), so its cell text isn't readable the same way a
        // DataGridTextColumn's is — the toggle button's own label above is
        // what actually proves IsReminded flipped.
        AppFixture.WaitUntil(() => app.Grid("AppointmentsRemindersGrid").RowCount == before, "the row to stay on the list");
        AppFixture.WaitUntil(() => app.Grid("AppointmentsRemindersGrid").Rows.Any(r => r.Cells[1].Value == name), "the reminded row to still be there");

        // Toggle it back off.
        SelectRowFor(app.Grid("AppointmentsRemindersGrid"), name);
        AppFixture.WaitUntil(() => app.Button("AppointmentsMarkReminded").Name == "Mark not reminded", "a reminded row to offer undoing it");
        app.Click("AppointmentsMarkReminded");

        SelectRowFor(app.Grid("AppointmentsRemindersGrid"), name);
        AppFixture.WaitUntil(() => app.Button("AppointmentsMarkReminded").Name == "Mark reminded", "the undo to have taken effect");
    }

    /// <summary>The "Due within" field is an editable combo — pickable from
    /// a preset list or typed over directly.</summary>
    [Fact]
    public void The_reminder_lead_days_field_accepts_a_typed_value_outside_the_presets()
    {
        EnsureAppointmentsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = BookForTomorrow(suffix, "09:00");

        app.SelectTab("AppointmentsTabs", "Reminders");
        AppFixture.WaitUntil(() => app.Find("AppointmentsReminderLeadDays") is not null, "the Reminders tab to render");

        // 0 days excludes tomorrow's booking outright.
        app.Type("AppointmentsReminderLeadDays", "0");
        AppFixture.WaitUntil(
            () => !app.Grid("AppointmentsRemindersGrid").Rows.Any(r => r.Cells[1].Value == name),
            "the too-short lead time to exclude tomorrow's booking");

        // A value typed in rather than picked from the preset list still works.
        app.Type("AppointmentsReminderLeadDays", "5");
        AppFixture.WaitUntil(
            () => app.Grid("AppointmentsRemindersGrid").Rows.Any(r => r.Cells[1].Value == name),
            "the wider, typed-in lead time to include tomorrow's booking again");
    }

    /// <summary>
    /// Regression test: a real front-desk workflow never leaves the
    /// Appointments page between booking something and checking it landed —
    /// they just switch tabs. Before this fix, every tab but the one active
    /// when the page was first navigated to showed stale (usually empty)
    /// data, which read as "the date field/reminders aren't working" even
    /// though the underlying booking succeeded.
    /// </summary>
    [Fact]
    public void Switching_tabs_within_the_page_reloads_each_tabs_own_data()
    {
        EnsureAppointmentsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");

        app.Navigate("NavAppointments", "Appointments");
        app.SelectTab("AppointmentsTabs", "Book appointment");
        var name = RegisterAndSelectPatient(suffix);

        var tomorrow = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
        app.Type("AppointmentsBookingDate", tomorrow);
        app.Type("AppointmentsBookingTime", "17:00");
        app.Click("AppointmentsBook");
        AppFixture.WaitUntil(
            () => app.TextOf("AppointmentsBookingStatus").Contains("booked", StringComparison.OrdinalIgnoreCase),
            "the booking confirmation");

        // Switch tabs without ever leaving the page — no Navigate() in between.
        app.SelectTab("AppointmentsTabs", "Today's appointments");
        app.SetDate("AppointmentsSelectedDate", DateTime.Today.AddDays(1));
        AppFixture.WaitUntil(
            () => app.Grid("AppointmentsTodayGrid").Rows.Any(r => r.Cells[1].Value == name),
            "the just-booked appointment to show up on tomorrow's list without leaving the page");

        app.SelectTab("AppointmentsTabs", "Reminders");
        AppFixture.WaitUntil(
            () => app.Grid("AppointmentsRemindersGrid").Rows.Any(r => r.Cells[1].Value == name),
            "the just-booked appointment to show up as a reminder without leaving the page");
    }

    /// <summary>
    /// Regression test: GetDoctorsAsync returns fresh entity instances on
    /// every call, so a second visit to this page used to leave
    /// SelectedDoctor pointing at an object no longer contained in the
    /// freshly rebuilt Doctors list — the combo would show blank even though
    /// a doctor was technically still "selected" underneath. Revisiting the
    /// page must keep the same doctor visibly selected.
    /// </summary>
    [Fact]
    public void Revisiting_the_page_keeps_the_previously_selected_doctor_selected()
    {
        EnsureAppointmentsEnabled();

        app.Navigate("NavAppointments", "Appointments");
        app.SelectTab("AppointmentsTabs", "Book appointment");
        AppFixture.WaitUntil(() => app.Find("AppointmentsDoctor") is not null, "the Book tab to render");

        var doctorCombo = app.ComboBox("AppointmentsDoctor");
        AppFixture.WaitUntil(() => doctorCombo.SelectedItem is not null, "a doctor to be pre-selected");
        var doctorName = doctorCombo.SelectedItem!.Text;

        app.Navigate("NavPatients", "Patients");
        app.Navigate("NavAppointments", "Appointments");
        app.SelectTab("AppointmentsTabs", "Book appointment");

        AppFixture.WaitUntil(
            () => app.ComboBox("AppointmentsDoctor").SelectedItem?.Text == doctorName,
            "the same doctor to still read as selected after revisiting the page");
    }

    [Fact]
    public void Booking_and_printing_opens_a_preview_that_can_be_closed()
    {
        EnsureAppointmentsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var tomorrow = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");

        app.Navigate("NavAppointments", "Appointments");
        app.SelectTab("AppointmentsTabs", "Book appointment");
        RegisterAndSelectPatient(suffix);

        app.Type("AppointmentsBookingDate", tomorrow);
        app.Type("AppointmentsBookingTime", "13:00");
        app.Click("AppointmentsBookAndPrint");

        var preview = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;
        Assert.NotNull(preview);

        preview!.FindFirstDescendant(cf => cf.ByAutomationId("PreviewClose"))?.AsButton().Invoke();
        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 0, "the preview to close");
    }
}
