using Pharma.Automation;

namespace Pharma.UiTests;

/// <summary>
/// Each test class gets its own application and its own database.
///
/// A single instance shared by every class was cheaper to launch but made the
/// suite unrepeatable: one test failing with a modal open left it on screen for
/// the next class, and data created by one class changed what another class saw.
/// Classes still run one at a time — UI Automation drives one desktop.
/// </summary>
public abstract class UiTestBase(AppFixture app) : IClassFixture<AppFixture>
{
    protected readonly AppFixture App = app;
}
