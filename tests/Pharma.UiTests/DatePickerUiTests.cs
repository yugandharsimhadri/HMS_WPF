using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;

namespace Pharma.UiTests;

/// <summary>
/// The date picker carries a custom control template, and its calendar popup is
/// built by WPF at click time — a template part in the wrong place only shows up
/// here, never at compile time.
///
/// Not covered: typing a date with the keyboard. The field takes keyboard focus
/// correctly (verified), but this harness cannot deliver synthetic keystrokes to
/// the application, so that path is untested rather than known-good.
/// </summary>
[Collection("ui")]
public class DatePickerUiTests(AppFixture app)
{
    [Fact]
    public void The_calendar_popup_opens_and_the_app_stays_responsive()
    {
        app.Navigate("NavOpd", "OPD");

        var picker = app.Require("OpdDate");
        var dropDown = picker.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button))?.AsButton();

        Assert.NotNull(dropDown);
        dropDown!.Invoke();

        // The calendar is created and parented into the popup only on first open.
        var calendar = Retry.WhileNull(
            () => app.MainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Calendar)),
            TimeSpan.FromSeconds(10)).Result;

        Assert.NotNull(calendar);

        dropDown.Invoke();
        app.Navigate("NavReports", "Reports");
        Assert.Equal("Reports", app.TextOf("PageTitle"));
    }

    [Fact]
    public void The_date_field_accepts_keyboard_focus()
    {
        app.Navigate("NavOpd", "OPD");

        var field = app.Require("OpdDate")
                       .FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))!;

        field.Focus();

        Assert.True(field.Properties.HasKeyboardFocus.ValueOrDefault,
            "The date field did not take keyboard focus.");
    }
}
