using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace Pharma.UiTests;

/// <summary>
/// The Cancel button on an OPD tile has to stop offering itself once the visit
/// has moved past the point where cancelling is honest — money taken, or the
/// doctor finished. The rule is enforced in <c>OpdService</c> (covered by
/// <c>OpdCancelTests</c>); this pins the half of it the operator actually sees,
/// which is the button going grey.
/// </summary>
public class OpdCancelUiTests(AppFixture app) : IClassFixture<AppFixture>
{
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

    private AutomationElement Cancel(string list, string name)
        => app.TileAction(list, "TileCancel", name)
           ?? throw new InvalidOperationException($"{name} has no Cancel button on {list}");

    [Fact]
    public void An_unpaid_waiting_visit_can_still_be_cancelled()
    {
        var name = $"UI Cancellable {DateTime.Now:HHmmssfff}";
        OpdUiTests.BookWalkIn(app, name, "9876500101", "33");
        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", name), "the tile to appear");

        Assert.True(Cancel("OpdWaitingList", name).IsEnabled,
            "a booking with nothing done to it yet is exactly what Cancel is for");
    }

    [Fact]
    public void Cancel_is_refused_once_the_fee_has_been_taken()
    {
        var name = $"UI Paid {DateTime.Now:HHmmssfff}";
        OpdUiTests.BookWalkIn(app, name, "9876500102", "45");
        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", name), "the tile to appear");

        app.TakeFee("OpdWaitingList", name);
        ClosePreview();

        // There is a numbered receipt in the patient's hand by now; cancelling
        // would leave it pointing at a visit that officially never happened.
        AppFixture.WaitUntil(() => !Cancel("OpdWaitingList", name).IsEnabled,
                             "the Cancel button to go grey once the fee is in");
    }
}
