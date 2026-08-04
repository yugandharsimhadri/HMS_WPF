# Sivaayaan HMS — HMS_WPF

A small Windows desktop application for a clinic with an attached pharmacy.
Two modules only: **OPD** and **Pharmacy**.

**[User guide](docs/USER_GUIDE.md)** — how to run a day on it, with screenshots.

Completely independent of the HMS web solution — no shared projects, no shared
database, no API. One executable, one SQLite file, no server to install.

---

## Running it

```bash
dotnet run --project src/Pharma.App
```

First launch creates and seeds the database at
`C:\HMS\DB\ShivayaanHMS.db` — one doctor and six common medicines,
so the counter works immediately. A database left by the pre-branding
`ClinicDesk` build, or by this application under its old name and database
file (`twinkle.db`), is carried over automatically on first launch.

```bash
dotnet test
```

Runs both suites: 13 unit tests over the GST and stock logic, and 17 UI tests
that launch the real window and drive it through Windows UI Automation. The UI
tests point the app at a throwaway database via the `CLINICDESK_DB` environment
variable, so they never touch the live one.

## Shipping it

```bash
dotnet publish src/Pharma.App -c Release -r win-x64 --self-contained
```

Produces a folder that runs on any Windows 10/11 PC with no .NET runtime
installed. Copy it, make a shortcut to `ShivayaanHMS.exe`, done.

---

## Projects

| Project | Depends on | Holds |
|---|---|---|
| `Pharma.Core` | *nothing* | Entities, enums, `GstCalculator` |
| `Pharma.Data` | Core | `AppDbContext`, migrations, services |
| `Pharma.App` | Core, Data | WPF views, view models, printing |
| `Pharma.Tests` | Core, Data | 13 unit tests over GST and the counter |
| `Pharma.UiTests` | App, Data | 17 UI tests driving the real window (FlaUI) |

`Pharma.Core` deliberately references nothing. If a web API, a second terminal
or a reporting tool is ever needed, it references Core and Data and inherits the
whole domain — no rewrite.

---

## What the screens do

**OPD** — today's queue on the left, booking on the right in three steps: find
the patient (or add them inline), pick the doctor and time, book. Booking and
visit are one record, so a patient who turns up is never re-keyed. Selecting a
row gives Mark arrived / Open consultation / Fee received / Cancel.

**Consultation** — vitals, complaint, diagnosis, advice, follow-up date, and an
editable prescription grid. Prints an A4 prescription with the clinic header and
the doctor's registration number.

**Patients** — the register behind OPD: search, edit, allergies, and the full
visit history of the selected patient with diagnosis and prescription count. A
patient with visits on record cannot be removed.

**Pharmacy counter** — type the medicine, the nearest-expiry batch with stock is
selected automatically, set the quantity, add. Live GST totals on the right,
payment mode, save and print. Today's OPD prescriptions can be pulled into a
bill in one click.

**Medicines** — the catalogue on the left, two forms on the right: medicine
details, and stock intake (batch, expiry, quantity, free quantity, rate, MRP).
A new drug goes from unknown to sellable without leaving the screen.

**Reports** — day book, GST summary, OPD register, expiring soon, low stock, and
the Schedule H1 register.

**Settings** — shop name, address, GSTIN, drug licence number, pharmacist, bill
footer, and the doctor list.

---

## Design decisions worth knowing

**MRP is GST-inclusive.** Indian medicines sell at the printed MRP, and that
price already contains the tax. `GstCalculator` back-calculates it:

```
taxable = net × 100 / (100 + gstRate)
gst     = net − taxable
cgst = sgst = gst / 2
```

Tax is never added on top of MRP. The bill total is rounded to the nearest rupee
with an explicit round-off line, and `GstCalculator.Bill` guarantees
`sum(lines) + roundOff == net`.

**Price and expiry live on the batch, not the medicine.** Two consignments of
the same drug have different MRPs and different expiry dates, and the bill must
show what was actually dispensed. Stock enters only through *Add stock*, which
creates a batch.

**Nearest expiry sells first.** Batches are offered in expiry order, and expired
batches are refused at the counter.

**A sale is all-or-nothing.** Stock validation, deduction, GST, bill numbering
and the H1 register happen in one transaction. An over-sell throws before
anything is written.

**Walk-in sales need no patient record.** `Sale.PatientId` is nullable and the
customer defaults to "Cash" — most counter sales have no file.

**Schedule H1 sales are auto-registered.** Selling an H1 drug writes a row to
`H1RegisterEntry` with patient, prescriber, batch and quantity. Statutory, and
retained for three years.

**Bill numbers are gap-free.** The counter increments inside the same
transaction as the bill that consumes it.

**No login.** Single PC, single operator. Add authentication when a second
terminal appears, not before.

**Every bill is settled in full.** The clinic takes no credit and no part
payment, so `PaymentMode` offers Cash, UPI and Card only. A Credit option existed
briefly and recorded nothing — a bill could look paid in the day book while the
money was never collected — so it was removed rather than left as a trap. There
is no receivable, no balance, and nothing to reconcile anywhere in the system.

**One `DbContext` per operation.** WPF has no request scope, so the app uses
`IDbContextFactory` and each service call opens and disposes its own context.
This avoids the stale-data and change-tracker problems a long-lived context
causes in desktop apps.

**Every interactive control carries an `AutomationProperties.AutomationId`.**
That is what makes the UI suite readable (`app.Click("OpdBook")`) instead of
brittle coordinate or label matching. Add the ID when you add the control.

**Nothing crashes the app to the desktop.** Unhandled dispatcher exceptions are
logged, shown as a plain message, and swallowed so a half-typed bill survives;
unobserved task failures and fatal exceptions are logged too. Every
fire-and-forget call goes through `Task.Forget(context)` rather than `_ =`, so a
background failure lands in the log instead of vanishing.

**Logging.** Plain text, one file per day, in
`C:\ProgramData\TwinkleHMS\logs\twinkle-yyyyMMdd.log`, 30 days retained. It
records startup, the database path, migrations applied, every bill and stock
entry, every visit booked, and every error with its stack trace. Settings shows
the current path and has an **Open log folder** button — that file is what to
attach when reporting a problem.

**Backups.** One copy of the database per day into
`C:\ProgramData\TwinkleHMS\backups`, keeping the last 14. A failed backup never
blocks startup.

---

## Not in this version

Sales returns / credit notes · purchase returns · inter-state IGST · e-invoicing
· multi-terminal · narcotic (Schedule X) register · GSTR-1 export file.

The GST arithmetic and the invoice layout are correct for a retail counter, but
this is not a certified e-invoicing integration. Have a CA review the GST
summary before it feeds a return, and confirm register formats with your local
drug inspector.
