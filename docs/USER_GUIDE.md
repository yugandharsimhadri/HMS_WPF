# Twinkle Children's Hospital — user guide

For the front desk, the doctor, and the pharmacy counter.

Every screenshot here is taken from the running application. To refresh them
after a change, run the one test that produces them:

```bash
dotnet test tests/Pharma.UiTests --filter ScreenshotCapture
```

---

## Contents

1. [Before the first patient](#1-before-the-first-patient)
2. [The OPD desk](#2-the-opd-desk)
3. [Seeing a patient](#3-seeing-a-patient)
4. [Medicines and stock](#4-medicines-and-stock)
5. [The pharmacy counter](#5-the-pharmacy-counter)
6. [Printing and reprinting](#6-printing-and-reprinting)
7. [Patient records](#7-patient-records)
8. [End of day](#8-end-of-day)
9. [Backups, logs, and problems](#9-backups-logs-and-problems)

---

## 1. Before the first patient

Open **Settings** and fill in the clinic's details. These print on every bill,
receipt and prescription, so get them right once.

![Settings](images/settings.png)

| Field | Why it matters |
|---|---|
| Clinic name, address, phone | Heading of every printed document |
| **GSTIN** | Required on a tax invoice |
| **Drug licence no** | Required on a retail chemist's bill |
| Pharmacist | Printed at the foot of the bill |
| OPD queue layout | Tiles or rows — see [section 2](#choosing-tiles-or-rows) |

Below that, add your **doctors**. At least one is needed before any visit can be
booked. The consultation fee entered here becomes the default when booking.

The app works with these left blank — the GSTIN line simply won't print — but
fill them in before issuing a real bill.

---

## 2. The OPD desk

![OPD queue](images/opd-tiles.png)

The screen shows one day at a time. **Waiting** on the left, **Completed** on
the right, and a tab for each doctor across the top. Pick a doctor's tab to see
only their patients, or **All doctors** for the whole clinic.

Each waiting patient shows their token number, name, age and sex, the time they
were booked, what they came in with, whether the fee has been taken, and how
long they have been waiting.

### Booking a visit

Click **+ New visit**.

![Booking a visit](images/opd-booking.png)

Three steps, in order:

1. **Find the patient.** Type a name or a phone number and press Enter. If the
   family already exists, everyone on that number is listed — **select the child
   who is actually here.** If nobody matches, the new-patient form opens with
   what you typed already filled in.
2. **Doctor and time.** The doctor defaults to whichever tab you were on, and
   the fee to that doctor's usual charge.
3. **Complaint**, if you want it on the queue tile and the prescription.

Then **Book visit**. A token number is allocated automatically.

> **One phone, several children.** A parent's number covers the whole family.
> Typing it lists every child registered against it, in any format — `9008007001`,
> `+91 90080 07001` and `90080 07001` all find the same family. If you click
> **Book visit** without picking one, the app stops you rather than creating a
> duplicate child.

### Taking the consultation fee

Click **Fee** on the patient's tile. A numbered receipt (`RCP00001`) is issued
and the print preview opens. The badge on the tile changes to **Fee paid**.

Change the payment method in the **Fee taken as** box in the booking panel
before clicking Fee. Clicking Fee twice does nothing — it will not issue a
second receipt.

### Moving a patient to Completed

Three ways, all equivalent:

- **Done** on the tile — for a patient who left without a full consultation
- Finishing a consultation with **Save & complete**
- **Reopen** on a completed tile moves them back to waiting

### Choosing tiles or rows

Set this in Settings. Tiles are easier to read across a room; rows fit more
people on screen when the clinic is busy. Everything works the same either way.

![OPD queue as rows](images/opd-rows.png)

---

## 3. Seeing a patient

Click **Consult** on the tile.

![Consultation](images/consultation.png)

Vitals on the left, prescription on the right. Nothing is compulsory — record
what you actually took.

For each prescription line, pick the medicine from the catalogue where you can,
rather than typing it free-hand. That lets the pharmacy pull the prescription
straight onto a bill.

At the bottom:

- **Save** — keeps the notes, patient stays in the waiting column
- **Print prescription** — saves, then opens the preview
- **Save & complete** — saves and moves the tile to Completed

---

## 4. Medicines and stock

![Medicines](images/medicines.png)

The catalogue is on the left, two forms on the right.

**Medicine details** — name, manufacturer, pack size, HSN code, GST rate,
schedule (H, H1, X), rack location and reorder level. Only the name is required.

**Add stock** — this is the *only* way stock enters the system, and it always
creates a batch:

| Field | Notes |
|---|---|
| Batch no | Printed on the pack. Required — it goes on the bill by law |
| Expiry | The pack is good until the end of that month |
| Qty / Free | Free is the scheme quantity, the "+1" in 10+1. It adds to stock |
| Rate | What the hospital pays |
| **MRP** | What the customer pays. The counter prices from this |

> **Price and expiry belong to the batch, not the medicine.** Two deliveries of
> the same drug can have different MRPs and different expiry dates, and the bill
> has to show what was actually handed over. That is why there is no single
> "price" field on a medicine.

---

## 5. The pharmacy counter

![Pharmacy counter](images/counter.png)

Per line, three steps: **find the medicine, set the quantity, add**.

Type part of the name and press Enter. Pick from the list — it shows the pack,
maker, rack and how many are in stock. The **nearest-expiry batch with stock is
selected for you**, which is the order stock should leave the shelf in.

Set the quantity, optionally a line discount, then **Add to bill**.

The totals on the right update as you go:

```
Gross            what the MRP comes to
Discount         anything taken off
Taxable value    the MRP with GST taken back out of it
CGST + SGST      the tax, split in half
Round off        to the nearest rupee
NET PAYABLE      what the customer hands over
```

> **MRP already includes GST.** Tax is never added on top. Ten strips at ₹112
> MRP come to exactly ₹1,120 — of which ₹1,000 is the taxable value and ₹120 is
> GST. If you have ever seen software bill this as ₹1,254, that is the mistake
> this avoids.

To dispense a prescription, choose the patient under **Or pull today's OPD
prescription** and click **Load prescription** — every prescribed medicine that
is in stock lands on the bill. Anything missing is named so you can tell the
parent.

Finish with **Save & print bill**, or **Save without printing**. The counter
clears itself for the next customer.

The app refuses to sell more than you have, and refuses expired batches.

---

## 6. Printing and reprinting

Every document previews before anything reaches paper.

![Print preview](images/print-preview.png)

Use **Print** to send it, or **Close** to go back. If no printer is set up the
app says so plainly rather than failing — you can still preview.

Three documents:

| Document | Number | Where from |
|---|---|---|
| Tax invoice (medicines) | `INV00001` | Counter, Reports day book, patient record |
| Consultation receipt | `RCP00001` | OPD tile, patient record |
| Prescription | `V00001` | Consultation window, OPD tile, patient record |

**Anything can be reprinted at any time**, however long ago it was. A reprint is
stamped **DUPLICATE** so it cannot be mistaken for the original.

---

## 7. Patient records

![Patients](images/patients.png)

Search by name, patient number, or phone. Selecting a patient shows their
details on the right and their whole history below, in two tabs:

- **Visits & prescriptions** — every visit, with diagnosis, receipt number and
  buttons to print the prescription or the fee receipt
- **Medicine bills** — every bill, with a button to print it again

This is where you go when someone comes back weeks later having lost a receipt.
A patient who has visits on record cannot be deleted.

---

## 8. End of day

![Reports](images/reports.png)

Across the top: pharmacy sales, split by cash and UPI, consultation fees
collected, and the number of OPD visits.

Six tabs below:

| Tab | What it is for |
|---|---|
| **Day book** | Every bill for the day. Also searches **all dates** by bill number or customer name |
| **GST summary** | Taxable value, CGST and SGST grouped by rate — what a return needs |
| **OPD register** | Every visit, diagnosis, fee and whether it was paid |
| **Expiring soon** | Batches within 90 days of expiry — return these to the distributor |
| **Low stock** | Anything at or below its reorder level |
| **Schedule H1 register** | Statutory record of H1 sales. Keep for three years |

---

## 9. Backups, logs, and problems

**Your data** lives in one file: `C:\ProgramData\TwinkleHMS\twinkle.db`. Copy
that file and you have copied the clinic.

**Backups** happen automatically — one copy per day into
`C:\ProgramData\TwinkleHMS\backups`, keeping the last 14. That protects against
mistakes, not against the PC dying. **Copy the folder to a pen drive weekly.**

**If something goes wrong,** the app tells you what happened and keeps running
rather than closing on you. Every error is written to
`C:\ProgramData\TwinkleHMS\logs`. Settings has an **Open log folder** button —
send that day's file when reporting a problem.

### Things the app deliberately refuses

| It says | Because |
|---|---|
| "Only 5 left of …" | You cannot sell stock you do not have |
| "Batch … expired on …" | Expired medicine cannot be dispensed |
| "3 people match that. Select which one" | Siblings share a phone; picking the wrong child is worse than a second click |
| "This patient has visits on record" | Deleting them would orphan their bills |
| "No MRP" on import | The counter prices from MRP and cannot sell without one |

### Not in this version

Sales returns and credit notes, purchase returns, inter-state IGST,
e-invoicing, multi-terminal use, and the Schedule X narcotic register.

The GST arithmetic and the invoice layout are correct for a retail counter, but
this is not a certified e-invoicing integration. Have a CA review the GST
summary before it feeds a return, and confirm register formats with your local
drug inspector.
