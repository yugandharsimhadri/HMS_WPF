# Pharma.DemoRunner

## Purpose

`Pharma.DemoRunner` is a standalone console utility that drives the real HMS
application for product demonstrations (e.g. demo video recording). It is not
a test project, is not part of the regression suite, and its exit code is the
only thing that indicates success or failure — there are no assertions.

Regression testing and demo recording want opposite lifecycles:

- **Regression testing** (`Pharma.UiTests`) launches HMS once per test class,
  runs that class's tests, and closes HMS — repeated ~24 times per full run.
  This isolation is deliberate: a modal left open by one class, or data left
  behind, must never leak into the next class.
- **Demo recording** wants the opposite: launch HMS once, run several
  workflows back to back, and close once at the end, so the recording never
  shows the application restarting between workflows.

`Pharma.DemoRunner` exists to serve the second lifecycle without touching the
first.

## Architecture

Both execution models drive the same application through the same UI
Automation code. That shared code was extracted into a third project,
`Pharma.Automation`, so that:

- `Pharma.UiTests` is never referenced by `Pharma.DemoRunner` — a test project
  (xUnit, `[Fact]`/`[Theory]`, coverlet, `Xunit.StaFact`) has no business being
  a dependency of a non-test tool.
- `Pharma.Automation` itself carries no test-framework dependency. It contains
  only `AppFixture` (launches HMS, waits for the main window, exposes
  `Find`/`Click`/`Type`/`Navigate`/`WaitUntil`, etc., and disposes the process)
  and, so far, one extracted business workflow: `PatientRegistrationWorkflow`.

```
HMS_WPF
├── src
│   ├── Pharma.App          (the app under demonstration)
│   ├── Pharma.Core
│   └── Pharma.Data
├── Pharma.Automation        (shared: AppFixture + extracted workflows)
├── tests
│   ├── Pharma.Tests
│   └── Pharma.UiTests        → references Pharma.Automation
└── tools
    ├── IconGen               (not in the .sln — ad hoc, run by hand)
    └── Pharma.DemoRunner     → references Pharma.Automation, added to the .sln
```

`Pharma.UiTests` used to own `AppFixture` directly. It now references
`Pharma.Automation` (via `ProjectReference` + a global `Using`, so no test
file needed to change its `using` directives) and gets the identical class
from there — same behavior, same file contents, different assembly.

`DiagnosticsUiTests`' private `NewPatient` helper — the one place in the
regression suite that registers a patient purely as test setup, not as the
thing under test — now delegates to `PatientRegistrationWorkflow.Register`
instead of repeating the steps inline. No other workflow was touched or
extracted; every other test class still drives the UI directly through
`AppFixture`, exactly as before.

## Execution flow (Phase 1)

```
dotnet run --project tools/Pharma.DemoRunner
```

1. Construct `AppFixture` — launches `TwinkleHMS.exe` against a throwaway
   database and log directory (identical to what every UI test class does),
   and blocks until the main window and first page are ready.
2. Call `PatientRegistrationWorkflow.Register(app, name, phone)` — navigates
   to Patients, opens "+ New patient", fills the form, saves, and waits for
   the confirmation.
3. Dispose `AppFixture` — closes (and if necessary kills) the HMS process,
   deletes the throwaway database and log directory.
4. Return exit code `0` on success, `1` if any step throws.

There is no configuration file, no workflow selection, and no second
workflow in this phase — deliberately.

## Demo pacing

A workflow runs in about six seconds, which is correct, fast, and far too
quick to narrate: a voiceover explaining a module wants ten to twenty. The
runner therefore has two speeds, and the fast one is the default — a bare run
behaves exactly as it always has, in both duration and output.

| Variable | Default | Meaning |
|---|---|---|
| `HMS_DEMO_RUN_MODE` | `test` | `demo` paces the run for recording; anything else runs flat out |
| `HMS_DEMO_MIN_SECONDS_PER_WORKFLOW` | `12` | Least time a workflow stays on screen, in demo mode only |

```
HMS_DEMO_RUN_MODE=demo dotnet run --project tools/Pharma.DemoRunner
```

Pacing works by **holding, not by slowing**. Every click runs at full speed and
the workflow's own final screen is then held until the minimum is reached. The
alternative — putting a delay between actions — would make the product itself
look sluggish on camera, which is the opposite of the point. Workflows that
already take longer than the minimum are never cut short, and a workflow that
throws is not held at all: there is no sense lingering on an error, and the run
is about to report failure anyway.

`Starting {Name}...` and `Completed {Name}.` are a contract, not just logging —
the content engine that assembles the video reads them to map each workflow
onto the recording's timeline. Two consequences:

- `Completed` is printed **after** the hold, so the gap between these lines is
  the time the workflow was genuinely on screen.
- They are written as they happen, not buffered to the end. `Console.Out`
  auto-flushes even when stdout is a pipe (verified by timestamping arrival
  through one), so nothing extra is needed here — but note the lines end
  **CRLF** on Windows. A reader that does not translate or strip newlines will
  fail a `$`-anchored match against the stray `\r`.

Pacing is reported by the run itself: the effective minimum once at the start,
each hold as it happens with what the workflow actually took, and the total
held at the end.

## How this differs from Pharma.UiTests

| | Pharma.UiTests | Pharma.DemoRunner |
|---|---|---|
| Launches HMS | Once per test class (~24×/run) | Once per run |
| Framework | xUnit, `[Fact]`/`[Theory]`, assertions | None — plain `Main`, exit code only |
| Purpose | Prove behavior is correct | Show behavior working, continuously |
| Isolation | Strict — fresh app/DB per class | None needed — one continuous session |
| Depends on | `Pharma.Automation`, `Pharma.App`, `Pharma.Data` | `Pharma.Automation` only |
| In `HMS_WPF.slnx` | Yes | Yes |

Regression behavior is unchanged: `Pharma.Tests`, the other 23 UI test
classes, `AppFixture`'s public API, and the per-class launch/close lifecycle
were not modified — only relocated (`AppFixture`) or pointed at the shared
copy (`DiagnosticsUiTests.NewPatient`).

## Future roadmap (not implemented yet)

- Additional extracted workflows (OPD booking, consultation, billing, etc.),
  each following the same `Pharma.Automation` extraction pattern used for
  Patient Registration — one workflow at a time, not a bulk migration.
- A way to run several workflows in one session (Workflow 1 → 2 → 3 → close),
  once more than one workflow has been extracted.
- Anything related to recording, publishing, or external tooling (OBS,
  FFmpeg, Content Automation Studio, screenshots) is explicitly out of scope
  for `Pharma.DemoRunner` itself and untouched by this phase.
