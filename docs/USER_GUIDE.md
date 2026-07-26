# Twinkle Children's Hospital

## User guide

For the front desk, the doctor, and the pharmacy counter.

Every screenshot is taken from the running application. To refresh them all after
a change, run the one test that produces them:

```bash
dotnet test tests/Pharma.UiTests --filter ScreenshotCapture
```

---

## Contents

**Getting started**
1. [The window](#1-the-window)
2. [Settings — set this up first](#2-settings--set-this-up-first)

**The OPD desk**
3. [The OPD screen](#3-the-opd-screen)
4. [Booking a visit](#4-booking-a-visit)
5. [The consultation](#5-the-consultation)

**The pharmacy**
6. [Medicines and stock](#6-medicines-and-stock)
7. [Importing a supplier bill](#7-importing-a-supplier-bill)
8. [The pharmacy counter](#8-the-pharmacy-counter)

**Everything else**
9. [Patients](#9-patients)
10. [Reports](#10-reports)
11. [Printing](#11-printing)
12. [When something goes wrong](#12-when-something-goes-wrong)

---

# 1. The window

Six screens down the left. The one you are on is highlighted.

| Button | What it is for |
|---|---|
| **OPD** | The day's queue. Book visits, take fees, open consultations |
| **Patients** | Everyone ever registered, with their whole history |
| **Pharmacy counter** | Selling medicines |
| **Medicines** | The catalogue and stock |
| **Reports** | End of day, GST, expiry, low stock |
| **Settings** | Clinic details, doctors, screen layout |

The heading of every screen shows the screen name and a one-line summary
underneath — for example *"2 waiting · 1 completed · Sun, 26 Jul"*.

At the bottom left it reminds you where the data lives and that a backup is taken
each day the application is opened.

---

# 2. Settings — set this up first

Everything here prints on your bills, receipts and prescriptions.

![Settings](images/settings.png)

## Shop details (left card)

| Control | What to enter |
|---|---|
| **Clinic / shop name** | Printed largest, at the top of every document |
| **Address** | One line, under the name |
| **Phone** | Shown beside the address |
| **GSTIN** | Your GST number. **Required on a tax invoice** |
| **Drug licence no** | Your 20B/21B number. **Required on a chemist's bill** |
| **Pharmacist** | Printed at the foot of the bill |
| **OPD queue layout** | `Tiles` or `Rows` — see [section 3](#choosing-tiles-or-rows) |
| **Bill footer** | Free text at the bottom of a bill, e.g. "Get well soon" |
| **Save shop details** | Applies everything above. New documents use it immediately |

Below the button it shows the **database file** and the **activity log** path, with
an **Open log folder** button. You need those only if reporting a problem.

> The application works with these blank — the GSTIN line simply does not print.
> Fill them in before you issue a real bill to a customer.

## Doctors (right card)

| Control | What it does |
|---|---|
| **List** | Every doctor. Click one to edit it |
| **Name** | Appears as an OPD tab and on the prescription |
| **Speciality** | Printed under the doctor's name on the prescription |
| **Registration no** | Printed on the prescription. Required on a real one |
| **Default consultation fee** | Fills in automatically when booking for this doctor |
| **Save doctor** | Saves the one being edited |
| **+ New doctor** | Clears the form to add another |

**At least one doctor is needed before any visit can be booked.**

---

# 3. The OPD screen

![OPD queue](images/opd-tiles.png)

## Top row

| Control | What it does |
|---|---|
| **All doctors** tab | Shows every patient in the clinic |
| **Doctor tabs** | One per doctor. Shows only their patients |
| **+ New visit** | Opens the booking panel on the right |
| **Date** | Which day you are looking at. Defaults to today |

The doctor is a tab rather than a column on each patient, which is why the tiles
stay small.

## The two columns

**Waiting** — everyone still to be seen. **Completed** — everyone finished.

A patient moves from one to the other and can be moved back.

## What a waiting tile shows

| On the tile | Meaning |
|---|---|
| Green number | **Token number**. What you call out |
| Name | The patient |
| `4F · 08:55` | Age, sex, and the time they were booked |
| Grey line | What they came in with |
| `Fee paid` / `Fee due` | Green if the consultation fee is taken, amber if not |
| `just arrived` / `waiting 12m` | How long they have been sitting there |

## Buttons on a waiting tile

| Button | What it does |
|---|---|
| **Consult** | Opens the consultation window for this patient |
| **Fee** | Takes the consultation fee, issues a numbered receipt, offers to print it |
| **Done** | Moves the tile to Completed without a consultation |
| **Cancel** | Cancels the visit. Asks first |

## Buttons on a completed tile

| Button | What it does |
|---|---|
| **Rx** | Prints the prescription. Says so if there isn't one |
| **Receipt** | Prints the fee receipt again, marked DUPLICATE |
| **Reopen** | Moves the patient back to Waiting |

## Choosing tiles or rows

Set in Settings. Tiles are easier to read across a room; rows fit more people on
screen when the clinic is busy. **Everything works the same in either.**

![OPD queue as rows](images/opd-rows.png)

---

# 4. Booking a visit

Click **+ New visit**. The panel opens on the right, in three numbered steps.

![Booking a visit](images/opd-booking.png)

## Step 1 — find the patient

| Control | What it does |
|---|---|
| **Search box** | Type a name or a phone number. Enter also works |
| **Find** | Runs the search |
| **Results list** | Everyone matching. **Click the right one** |
| **+ New patient** | Opens the form below to register someone new |

If nobody matches, the new-patient form opens by itself with what you typed
already filled in.

| New patient field | Notes |
|---|---|
| **Name** | The only one that is required |
| **Phone** | The parent's number. Shared across siblings is normal |
| **Age** | In years |
| **Sex** | Male, Female or Other |
| **Back to search** | Returns to the list without adding anyone |

## Step 2 — doctor and time

| Control | Notes |
|---|---|
| **Doctor** | Defaults to whichever tab you were on |
| **Time** | 24-hour, e.g. `09:45`. Defaults to now |
| **Fee** | Defaults to that doctor's usual charge |

## Step 3 — complaint

Optional. Whatever you type shows on the queue tile and on the prescription.

Then **Book visit**. A token number is allocated automatically and the panel
closes. **Fee taken as** below the button sets the payment method used when you
later click **Fee** on the tile.

> ### One phone, several children
>
> A parent's number covers the whole family. Typing it lists **every child**
> registered against it, and the message says so: *"3 people are registered on
> this number. Select which one is here."*
>
> The number is matched on its digits, so `9008007001`, `+91 90080 07001` and
> `90080 07001` all find the same family.
>
> If you click **Book visit** without choosing one, the application **stops you**
> rather than quietly registering a fourth child who already exists.

---

# 5. The consultation

Click **Consult** on a tile.

![Consultation](images/consultation.png)

The heading shows the token, the patient, their age and sex, and the doctor.

## Left — clinical notes

| Control | Notes |
|---|---|
| **Weight kg**, **BP**, **Temp °F** | Vitals. All optional |
| **Complaint** | Carried over from booking; edit freely |
| **Diagnosis** | Printed in bold on the prescription |
| **Advice / notes** | Printed under the medicines |
| **Fee** | Can be changed here |
| **Review on** | Follow-up date. Printed as "Review on 01 Aug 2026" |

## Right — the prescription

Fill the form at the top and press **Add**. Each medicine then appears in the
list below.

| Control | Notes |
|---|---|
| **Medicine** | Type two letters or more to search our pharmacy. Matches appear underneath — click one to use it |
| **Dose** | e.g. `5 ml`, `1 tab` |
| **Frequency** | `1-0-1`, `1-1-1`, `1/2-0-1/2`, or `OD` `BD` `TDS` `QID` `SOS` |
| **Days** | Length of the course |
| | *Nothing is filled in for you — a dose is the doctor's decision, not the software's* |
| **Qty (units)** | **Individual tablets.** Worked out for you — change it if you want |
| Grey line under the fields | What the course comes to, in tablets and in strips |
| **Instructions** | e.g. "after food". Printed under the line |
| **Add** | Adds the medicine to the list |
| **✕** in the list | Removes that medicine |

Frequency and days are **kept after adding**, because a prescription usually
repeats the same course — only the medicine changes.

> ### Quantity is always in individual units
>
> You write `1-0-1` for `3` days and it fills in **6** — six tablets, not six
> strips. The line underneath shows what the pharmacy will hand over, e.g.
> *"6 units · 1 × 10 TAB minus 4"*, so there is never any doubt about whether a
> number means tablets or strips.
>
> `SOS` and `PRN` have no fixed daily dose, so nothing is filled in and you type
> the quantity yourself.

> ### Prescribing something the clinic does not stock
>
> Type the name and simply **do not pick anything from the list**. The line reads
> *"Not in our pharmacy — it will be written on the prescription only"*, and it is
> added exactly as typed. The parent buys it from an outside chemist.
>
> **It is never added to our medicine records.** A name typed on a prescription
> does not create a medicine, a batch, or a price — our catalogue only ever grows
> when stock is received.
>
> A medicine you **do** pick from the list shows *"In our pharmacy · 60 in stock"*
> and can be pulled straight onto a bill at the counter, with the quantity already
> correct. One prescription can mix the two freely.

## Bottom buttons

| Button | What it does |
|---|---|
| **Save** | Keeps everything. Patient stays in Waiting |
| **Print prescription** | Saves, then opens the print preview |
| **Save & complete** | Saves and **moves the tile to Completed** |

---

# 6. Medicines and stock

![Medicines](images/medicines.png)

## The catalogue (left)

Search box, **Search**, **Import bill**, **+ New medicine**. The grid shows
medicine, pack, maker, rack, GST %, schedule, units per pack, and stock on hand.
Click a row to load it into the forms on the right.

## Medicine details (top right)

| Field | Notes |
|---|---|
| **Name** | The only required field |
| **Manufacturer** | Company name |
| **Pack size** | As printed, e.g. `10 TAB`, `60ML` |
| **HSN** | Tax code. `3004` covers most formulations |
| **GST %** | Usually 5 or 12 |
| **Schedule** | `None`, `H`, `H1`, `X`. H1 sales are recorded in a register |
| **Rack** | Where it sits on the shelf. Searchable |
| **Reorder level** | Below this it appears in the Low stock report |
| **Units per pack** | **10 for a ten-tablet strip. 1 for a syrup bottle** |
| **Sell loose units** | Allows part of a strip to be sold |
| **Active** | Uncheck to hide it from the counter |
| **Save medicine** | Saves it |

> ### Units per pack is what makes loose sale work
>
> Set it to `10` for a strip of ten tablets and stock is counted in **tablets**,
> so a customer can buy five. Leave it at `1` for a syrup bottle — half a bottle
> is not a thing you can sell.

## Add stock (bottom right)

**This is the only way stock enters, and it always creates a batch.**

| Field | Notes |
|---|---|
| **Batch no** | Printed on the pack. **Required — it goes on the bill by law** |
| **Expiry** | The pack is good until the **end** of that month |
| **Qty** | How many **packs** arrived |
| **Free** | Scheme quantity, the "+1" in 10+1. Adds to stock, costs nothing |
| **Rate** | What the hospital paid per pack |
| **MRP** | The price printed on the pack. **The counter prices from this** |
| **Supplier**, **Supplier bill no** | For your records |
| **Add stock** | Adds it |

Below that, **Batches in stock** lists every batch of the selected medicine with
its expiry, MRP and quantity left.

> **Adding stock always adds.** Receiving the same batch number again increases
> what is on the shelf. It never replaces it.

## Correct the stock count

For when the shelf and the screen disagree — breakage, a miscount, or something
keyed in wrongly.

| Control | Notes |
|---|---|
| **Batch** | Which batch is wrong. Shows expiry, MRP and current count |
| **True count** | What is **actually** on the shelf, in units |
| **Reason** | `Recount`, `Breakage`, `Expired`, `Lost`, `Entry error`, `Other` |
| **Notes** | Free text — worth writing for anything unusual |
| **Correct count** | Applies it |
| **Recent corrections** | When, what, was, now, change, reason and notes |

> ### Every correction is recorded
>
> Stock otherwise only moves by receiving or selling, and both leave a document.
> A manual correction has none, so it writes its own — otherwise a shortfall
> looks the same as theft and nobody can say what happened.
>
> The correction and its record are saved together: you cannot get one without
> the other. Setting the count to what it already is is refused, and stock cannot
> go below zero.

---

# 7. Importing a supplier bill

Instead of keying in a delivery line by line, load the file your supplier sends.
**Medicines → Import bill.**

![Import a supplier bill](images/import.png)

## Step 1 — choose the profile and the file

| Control | What it does |
|---|---|
| **Supplier profile** | Which supplier's format this is. Each knows that supplier's date and expiry style |
| **File** | The path. Use Browse |
| **Supplier name** | **Type this** — the file does not contain it |
| **Browse…** | Pick the file. Reads it immediately |
| **Read file** | Re-reads it after changing the profile |

## Step 2 — check what it will do

The summary line reads, for example:
*"Bill SW02236 dated 04 Jul 2026 · 9 line(s) · 9 new medicine(s) · 1042 unit(s) · net ₹15334.00"*

| Column | Meaning |
|---|---|
| **STATUS** | `Matched` — found in your catalogue · `Check` — a likely match, confirm it · `New medicine` — will be created |
| **MEDICINE**, **PACK**, **BATCH**, **EXPIRY** | As the supplier sent them |
| **PER PACK** | **Editable.** Units in one pack |
| **QTY**, **FREE** | Packs billed, and free packs |
| **UNITS** | What will land on the shelf: (qty + free) × per pack |
| **RATE**, **MRP**, **GST** | Cost, printed price, tax rate |

**What the file says** below lists everything worth knowing — the bill date it
read, MRP changes, anything it could not understand.

> ### Check the PER PACK column
>
> The file rarely states how many are in a pack. `30s` is read as 30 gummies.
> `60ML` is one bottle and stays `1` — a syrup is not sixty sellable units.
>
> For strips, **type the real number** before importing. Correct it once and the
> medicine remembers it.

## Step 3 — import

**Import** writes everything in one go. **Close** abandons it — nothing is
written until you click Import.

Afterwards it reports: *"Imported as GRN00003: 9 line(s), 9 new medicine(s), 1042
unit(s) added to stock."*

### What the import guarantees

- **Stock is added**, never replaced. 12 counted by hand plus 5 on the bill is 17
- **The same bill cannot be imported twice.** It is refused by bill number
- **Nothing is written if anything fails.** All of it, or none
- **The supplier's product codes are remembered**, so their next bill matches by itself

---

# 8. The pharmacy counter

![Pharmacy counter](images/counter.png)

Three steps per line: **find the medicine, set the quantity, add.**

## Add medicine to the bill

| Control | What it does |
|---|---|
| **Medicine** | Type part of the name. Enter searches |
| **Find** | Runs the search |
| **Results list** | Shows pack, maker, rack and stock. Click one |
| **Batch** | **The nearest-expiry batch with stock is chosen for you** |
| **Qty (units)** | **Tablets, not strips.** `5` sells five out of a strip |
| **Disc %** | Discount on this line |
| **Add to bill** | Adds the line |

## Bill items

| Column | Notes |
|---|---|
| **MEDICINE**, **BATCH**, **EXPIRY** | What is being handed over |
| **QTY** | Base units. Editable |
| **PACKS** | Reads back as `2 × 10 TAB + 3` |
| **MRP**, **DISC %** | Editable |
| **GST %**, **AMOUNT** | Calculated |
| **✕** | Removes the line |

## Customer (right)

| Control | Notes |
|---|---|
| **Name** | Defaults to `Cash`. **A walk-in needs no patient record** |
| **Prescribed by** | Doctor's name. Required on the bill for a Schedule H1 drug |
| **Or pull today's OPD prescription** | Pick a patient seen today |
| **Load prescription** | Puts every prescribed medicine that is in stock on the bill |

## Totals

```
Gross            what the MRP comes to
Discount         anything taken off
Taxable value    the MRP with GST taken back out of it
CGST + SGST      the tax, split in half
Round off        to the nearest rupee
NET PAYABLE      what the customer hands over
```

| Control | What it does |
|---|---|
| **Payment** | Cash, UPI or Card |
| **Save & print bill** | Saves and opens the preview |
| **Save without printing** | Saves only |
| **Clear bill** | Empties the counter without saving |

> ### Every bill is settled in full
>
> There is no credit and no part payment — by design, because the clinic does not
> offer them. A bill is paid before it is saved, so nothing is ever outstanding
> and there is no balance to chase.

> ### MRP already includes GST
>
> Tax is never added on top. Ten strips at ₹112 MRP come to **exactly ₹1,120** —
> of which ₹1,000 is the taxable value and ₹120 is GST.

> ### A whole strip always costs what is printed on it
>
> Five tablets from a ₹112 strip of ten cost ₹56. But a **full** strip costs
> ₹112, not ten times the rounded per-tablet price. This matters where the price
> does not divide evenly: ₹87.50 across fifteen tablets is ₹5.83 each, and the
> full strip still costs ₹87.50.

---

# 9. Patients

![Patients](images/patients.png)

Search by **name, patient number or phone**. A phone number lists the whole
family.

## The register (top left)

Patient no, name, phone, age, sex, allergies. Click a row to select it.

## Patient details (right)

| Field | Notes |
|---|---|
| **Patient no** | Allocated on save, e.g. `P00012` |
| **Name**, **Phone**, **Age**, **Sex** | |
| **Address**, **Allergies** | Optional |
| **Save patient** | Saves changes |
| **Remove** | Refused if they have visits on record |
| **+ New patient** | Registers someone without booking a visit |

## History (bottom left)

**Visits & prescriptions** — every visit ever, with diagnosis, fee and receipt
number. Select one, then:

- **Print prescription** — prints it however long ago it was
- **Print fee receipt** — prints the receipt again, marked DUPLICATE

**Medicine bills** — every bill for this patient, with **Print bill**.

> This is where you go when someone returns weeks later having lost a receipt.

---

# 10. Reports

![Reports](images/reports.png)

Across the top for the chosen date: pharmacy sales, cash, UPI, consultation fees
collected, and OPD visits.

| Tab | What it is for |
|---|---|
| **Day book** | Every bill for the day. **Find any bill** searches every date by bill number or customer name. **Reprint selected bill** prints it again |
| **GST summary** | Taxable value, CGST and SGST grouped by rate — what a return needs |
| **OPD register** | Every visit, diagnosis, fee, and whether it was paid |
| **Expiring soon** | Batches within 90 days of expiry. Return these to the distributor |
| **Low stock** | Anything at or below its reorder level |
| **Schedule H1 register** | Statutory record of H1 sales. **Keep for three years** |

---

# 11. Printing

Everything previews before any paper moves.

![Print preview](images/print-preview.png)

**Print** sends it. **Close** goes back. If no printer is set up the application
says so plainly instead of failing, and you can still preview.

| Document | Number | Print it from |
|---|---|---|
| Tax invoice (medicines) | `INV00001` | Counter · Reports day book · patient record |
| Consultation receipt | `RCP00001` | OPD tile · patient record |
| Prescription | `V00001` | Consultation · OPD tile · patient record |

**Anything can be reprinted at any time.** A reprint is stamped **DUPLICATE** so
it cannot be mistaken for the original.

---

# 12. When something goes wrong

## The application does not close on an error

If something unexpected happens it tells you in plain words, writes the details
to the log, and **keeps running**. A half-typed bill is not lost. You will see a
message like:

> *Taking the fee could not be completed. The change could not be saved — it may
> already exist, or a required field is missing. Nothing was changed.*

## Things it refuses on purpose

| It says | Why |
|---|---|
| "Only 3 × 10 TAB + 4 left of …" | You cannot sell stock you do not have |
| "Batch … expired on …" | Expired medicine cannot be dispensed |
| "3 people match that. Select which one" | Siblings share a phone; the wrong child is worse than a second click |
| "This patient has visits on record" | Deleting them would orphan their bills |
| "Bill … was already received on …" | Importing twice would double your stock |
| "No MRP" during import | The counter prices from MRP and cannot sell without one |
| "Batch number is required" | It has to appear on the bill by law |

## Your data

Everything is in one file: `C:\ProgramData\TwinkleHMS\twinkle.db`. Copy that file
and you have copied the clinic.

**Backups** are automatic — one per day into `C:\ProgramData\TwinkleHMS\backups`,
keeping the last 14. That protects against mistakes, **not against the PC dying**.
Copy the folder to a pen drive weekly.

**The log** is at `C:\ProgramData\TwinkleHMS\logs`, one file per day, 30 days
kept. Settings has an **Open log folder** button. Send that day's file when
reporting a problem.

## Not in this version

Sales returns and credit notes · purchase returns · inter-state IGST ·
e-invoicing · multi-terminal use · the Schedule X narcotic register.

**Credit and part payment are not missing — they are deliberately absent.** The
clinic settles every bill in full at the counter, so there is no outstanding
balance anywhere in the system and nothing to reconcile.

The GST arithmetic and the invoice layout are correct for a retail counter, but
this is not a certified e-invoicing integration. Have a CA review the GST summary
before it feeds a return, and confirm register formats with your local drug
inspector.
