namespace Pharma.Core;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

// ── OPD ────────────────────────────────────────────────────────────────────

public class Patient : BaseEntity
{
    public string PatientNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public Gender Gender { get; set; } = Gender.Male;
    public int Age { get; set; }
    public string? Address { get; set; }
    public string? Allergies { get; set; }

    public ICollection<Visit> Visits { get; set; } = [];

    public string Display => $"{Name} ({Age}{Gender.ToString()[0]}) · {Phone}";
}

public class Doctor : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? RegistrationNo { get; set; }
    public string? Speciality { get; set; }
    public string? Phone { get; set; }
    public decimal ConsultationFee { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// One OPD appointment. Booking and visit are deliberately the same record — a
/// booking that the patient turns up for simply moves Booked → Waiting, so the
/// front desk never re-keys anything.
/// </summary>
public class Visit : BaseEntity
{
    public string VisitNo { get; set; } = string.Empty;
    public int TokenNo { get; set; }
    public DateTime ScheduledOn { get; set; }
    public VisitStatus Status { get; set; } = VisitStatus.Booked;

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    public string? Complaint { get; set; }
    public string? Diagnosis { get; set; }
    public string? Notes { get; set; }

    // Vitals — all optional, entered only if the clinic bothers with them.
    public decimal? WeightKg { get; set; }
    public string? BloodPressure { get; set; }
    public decimal? TemperatureF { get; set; }

    public decimal Fee { get; set; }
    public bool FeePaid { get; set; }

    // A receipt has to carry its own number and date, or it cannot be reprinted
    // or reconciled against the day's collection.
    public string? FeeReceiptNo { get; set; }
    public DateTime? FeePaidOn { get; set; }
    public PaymentMode? FeePaymentMode { get; set; }

    public DateTime? FollowUpOn { get; set; }

    public ICollection<PrescriptionItem> Prescription { get; set; } = [];

    // ── Display helpers for the queue tiles ────────────────────────────────

    /// <summary>Still to be seen — everything that has not finished or been cancelled.</summary>
    public bool IsWaiting => Status is VisitStatus.Booked or VisitStatus.Waiting or VisitStatus.InConsultation;

    public string PatientLine => Patient is null
        ? ""
        : $"{Patient.Age}{Patient.Gender.ToString()[0]} · {ScheduledOn:HH:mm}";

    public string FeeBadge => FeePaid ? "Fee paid" : "Fee due";

    /// <summary>One line for the row layout, with no dangling separator when
    /// the complaint was left blank.</summary>
    public string RowSummary => string.IsNullOrWhiteSpace(Complaint)
        ? PatientLine
        : $"{PatientLine}  ·  {Complaint}";

    /// <summary>How long they have been sitting there, in words the desk uses.</summary>
    public string WaitedFor
    {
        get
        {
            if (!IsWaiting) return "";

            var minutes = (int)(DateTime.Now - ScheduledOn).TotalMinutes;
            return minutes switch
            {
                < 1 => "just arrived",
                < 60 => $"waiting {minutes}m",
                _ => $"waiting {minutes / 60}h {minutes % 60}m"
            };
        }
    }
}

public class PrescriptionItem : BaseEntity
{
    public Guid VisitId { get; set; }
    public Visit Visit { get; set; } = null!;

    /// <summary>Set when the drug was picked from the catalogue; null for free-text.</summary>
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    public string MedicineName { get; set; } = string.Empty;
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public int Days { get; set; }
    public int Quantity { get; set; }
    public string? Instructions { get; set; }
}

// ── Pharmacy ───────────────────────────────────────────────────────────────

public class Product : BaseEntity
{
    /// <summary>The brand on the pack — what a customer usually asks for.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The drug itself, e.g. "Paracetamol 500mg" behind the brand "Calpol".
    /// Searched alongside the brand, because staff know medicines by both.
    /// </summary>
    public string? GenericName { get; set; }

    public string? Manufacturer { get; set; }

    /// <summary>
    /// The salts and strengths, e.g. "Paracetamol 500mg + Caffeine 30mg". What a
    /// pharmacist needs to substitute a brand that has run out.
    /// </summary>
    public string? Composition { get; set; }

    /// <summary>
    /// "Below 25°C", "2–8°C, do not freeze". Matters in a children's clinic:
    /// vaccines, insulin and some syrups are ruined by a warm shelf.
    /// </summary>
    public string? Storage { get; set; }

    /// <summary>Printed pack, e.g. "10 TAB" or "100 ML". Free text — shops describe packs their own way.</summary>
    public string? PackSize { get; set; }

    /// <summary>What one sellable unit is — a tablet, a bottle, a sachet.</summary>
    public DispensingUnit DispensingUnit { get; set; } = DispensingUnit.Tablet;

    /// <summary>
    /// Sellable units in one pack. There is no standard: strips come in 1, 3, 5,
    /// 10, 15 and more, and a syrup bottle is 1. Stock is counted in units so part
    /// of a strip can be sold, and each batch keeps the count it arrived with.
    /// </summary>
    /// <remarks>
    /// Clamped, because zero is never a legitimate pack. Rows written before the
    /// guards existed hold 0, which showed as "PER PACK 0" and made every
    /// quantity derived from it meaningless. Reading it as 1 keeps the arithmetic
    /// honest until the repair sweep corrects the stored value.
    /// </remarks>
    public int UnitsPerPack
    {
        get => Math.Max(1, _unitsPerPack);
        set => _unitsPerPack = Math.Max(1, value);
    }

    private int _unitsPerPack = 1;

    /// <summary>"10 tablets per strip", or just "bottle" when a pack is one unit.</summary>
    public string PackDescription => UnitsPerPack > 1
        ? $"{UnitsPerPack} {DispensingUnit.Name(UnitsPerPack)} per pack"
        : $"sold as single {DispensingUnit.Name(1)}";

    /// <summary>Off for anything that must leave the shop whole.</summary>
    public bool AllowLooseSale { get; set; } = true;
    public string HsnCode { get; set; } = "3004";
    public decimal GstRate { get; set; } = 12m;
    public DrugSchedule Schedule { get; set; } = DrugSchedule.None;
    public string? RackLocation { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Brand, maker and pack, normalised — trimmed, case-folded, whitespace
    /// collapsed. A unique index sits on this, so "Cetirizine 10mg" and
    /// "cetirizine  10MG" from the same maker cannot both exist. Kept as a
    /// stored column because a database constraint is a guarantee and a check in
    /// the screen is only a suggestion.
    /// </summary>
    public string SearchKey { get; set; } = "";

    public static string KeyFor(string? name, string? manufacturer, string? packSize)
    {
        static string Norm(string? value) =>
            string.Join(' ', (value ?? "").Trim().ToLowerInvariant()
                                          .Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return $"{Norm(name)}|{Norm(manufacturer)}|{Norm(packSize)}";
    }

    public string BuildKey() => KeyFor(Name, Manufacturer, PackSize);

    public ICollection<Batch> Batches { get; set; } = [];

    public int StockOnHand => Batches.Where(b => !b.IsDeleted).Sum(b => b.QtyOnHand);

    /// <summary>The batch that would actually be dispensed — nearest expiry with stock.</summary>
    private Batch? NextBatch => Batches
        .Where(b => !b.IsDeleted && b.QtyOnHand > 0)
        .OrderBy(b => b.ExpiryDate)
        .FirstOrDefault();

    /// <summary>What one unit costs, so the counter can quote without opening anything.</summary>
    public string UnitPriceLabel => NextBatch is { } b
        ? $"₹{b.UnitPrice:0.00}"
        : "—";

    /// <summary>The pack price behind it, shown only when a pack is more than one unit.</summary>
    public string PackPriceLabel => NextBatch is { } b && b.UnitsPerPack > 1
        ? $"₹{b.Mrp:0.00} / pack"
        : "";

    /// <summary>Blank rather than a dangling "rack" when no location is recorded.</summary>
    public string RackLabel => string.IsNullOrWhiteSpace(RackLocation) ? "" : $"rack {RackLocation}";
    /// <summary>How far under the reorder level current stock has fallen. Zero when not low.</summary>
    public int Shortage => Math.Max(0, ReorderLevel - StockOnHand);

    /// <summary>
    /// How the shelf stands, for anything that has to show it at a glance.
    ///
    /// In a column of figures a "0" looks exactly like a "10", and whether
    /// there is any left is the one thing on that screen somebody has to
    /// notice. Running low only means anything once a reorder level has been
    /// set, so a medicine without one is never called low — only empty.
    /// </summary>
    public StockLevel Level => StockOnHand <= 0 ? StockLevel.Empty
                             : Shortage > 0 ? StockLevel.Low
                             : StockLevel.Fine;
}

public enum StockLevel { Fine, Low, Empty }

/// <summary>
/// Stock is always batch-wise: MRP and expiry are printed on the strip and differ
/// between consignments of the same drug, so both must come from the batch.
/// </summary>
public class Batch : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string BatchNo { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }

    /// <summary>The price printed on the pack. What a tax invoice must show.</summary>
    public decimal Mrp { get; set; }

    public decimal PurchaseRate { get; set; }

    /// <summary>Stock in base units, not packs.</summary>
    public int QtyOnHand { get; set; }

    /// <summary>
    /// Snapshotted from the product when the batch was received. A manufacturer
    /// changes pack size between consignments, and old stock has to keep pricing
    /// against the pack it actually came in.
    /// </summary>
    /// <remarks>Clamped for the same reason as on the product: zero is never a pack.</remarks>
    public int UnitsPerPack
    {
        get => Math.Max(1, _unitsPerPack);
        set => _unitsPerPack = Math.Max(1, value);
    }

    private int _unitsPerPack = 1;

    public string? SupplierName { get; set; }

    /// <summary>
    /// The supplier's bill this batch came in on. Without it the reconciliation
    /// list can show that something needs a bill but not whose, which is most of
    /// the work of reconciling.
    /// </summary>
    public string? SupplierInvoiceNo { get; set; }

    /// <summary>
    /// Packs received free on a scheme — the "+1" in 10+1. Captured at receiving
    /// and then lost, which overstated cost per unit on every margin figure: ten
    /// paid for and eleven received makes the real cost rate × 10 ÷ 11.
    /// </summary>
    public int FreePacks { get; set; }

    /// <summary>What a pack really cost, once free goods are counted.</summary>
    public decimal EffectivePackCost
    {
        get
        {
            var paid = QtyOnHand > 0 ? PurchaseRate : PurchaseRate;
            var packs = PacksReceived;

            return packs + FreePacks <= 0 ? paid : Round(paid * packs / (packs + FreePacks));
        }
    }

    /// <summary>Paid-for packs behind this batch, as far as it can be told.</summary>
    public int PacksReceived { get; set; }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public DateTime ReceivedOn { get; set; } = DateTime.Today;

    /// <summary>
    /// Entered at the counter because the medicine was on the shelf but not in
    /// the system, rather than received against a supplier bill. Purchases will
    /// not tie out against sales until it is reconciled, so it is flagged rather
    /// than hidden — see the Stock to reconcile report.
    /// </summary>
    public bool IsProvisional { get; set; }

    public bool IsExpired => ExpiryDate.Date < DateTime.Today;

    public decimal UnitPrice => PackMath.UnitPrice(Mrp, UnitsPerPack);

    public string OnHand => PackMath.Describe(QtyOnHand, UnitsPerPack, Product?.PackSize, Product?.DispensingUnit.Name(QtyOnHand));

    public string Display => $"{BatchNo} · exp {ExpiryDate:MM'/'yy} · ₹{Mrp:0.00} · {OnHand} left";

    /// <summary>
    /// What can go back to the distributor. A broken strip cannot — so a batch
    /// with 47 of a ten-strip is four returnable strips and seven to write off,
    /// and the expiry report has to say so in those terms.
    /// </summary>
    public string Returnable
    {
        get
        {
            if (UnitsPerPack <= 1) return $"{QtyOnHand} sealed";

            var sealed_ = QtyOnHand / UnitsPerPack;
            var open = QtyOnHand % UnitsPerPack;

            return open == 0
                ? $"{sealed_} sealed"
                : $"{sealed_} sealed + {open} loose to write off";
        }
    }

    /// <summary>Days until it expires. Negative once it has.</summary>
    public int DaysToExpiry => (int)(ExpiryDate.Date - DateTime.Today).TotalDays;
}

public class StockEntry : BaseEntity
{
    public string EntryNo { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; } = DateTime.Today;
    public string? SupplierName { get; set; }
    public string? SupplierInvoiceNo { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    // Set when the entry came from a vendor file rather than being keyed in.
    // The invoice number is what stops the same bill being imported twice.
    public string? ImportedFile { get; set; }
    public string? ImportProfile { get; set; }
    public decimal NetAmount { get; set; }
    public decimal DiscountPercent { get; set; }

    public bool WasImported => ImportedFile is not null;

    /// <summary>
    /// Keyed at the counter to get a sale through, without a supplier bill
    /// behind it. Kept as a first-class entry so the stock has a document, and
    /// flagged so it can be reconciled against the real bill when it turns up.
    /// </summary>
    public bool IsProvisional { get; set; }

    /// <summary>Who keyed it. Blank for anything received the normal way.</summary>
    public string? EnteredBy { get; set; }

    public ICollection<StockEntryItem> Items { get; set; } = [];
}

/// <summary>
/// A vendor's own code for a medicine. Two suppliers call the same drug 000071
/// and 31435, so the mapping is per vendor. Recording it on the first import is
/// what makes every later import of that vendor match without being asked.
/// </summary>
public class VendorProductCode : BaseEntity
{
    public string VendorProfile { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
}

public class StockEntryItem : BaseEntity
{
    public Guid StockEntryId { get; set; }
    public StockEntry StockEntry { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string BatchNo { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    /// <summary>Packs received, as the vendor billed them.</summary>
    public int Quantity { get; set; }

    /// <summary>Scheme quantity (the "+1" in 10+1). Adds to stock, costs nothing.</summary>
    public int FreeQuantity { get; set; }

    /// <summary>Units in each pack received; multiplies into base-unit stock.</summary>
    public int UnitsPerPack { get; set; } = 1;

    public decimal PurchaseRate { get; set; }
    public decimal Mrp { get; set; }

    public decimal LineTotal => Quantity * PurchaseRate;

    /// <summary>Base units this line puts on the shelf, free goods included.</summary>
    public int UnitsReceived => (Quantity + FreeQuantity) * Math.Max(1, UnitsPerPack);
}

public class Sale : BaseEntity
{
    public string BillNo { get; set; } = string.Empty;
    public DateTime BillDate { get; set; } = DateTime.Now;

    /// <summary>Null for a walk-in cash sale — most counter sales have no patient record.</summary>
    public Guid? PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>Set when the bill was raised from an OPD prescription.</summary>
    public Guid? VisitId { get; set; }
    public Visit? Visit { get; set; }

    public string CustomerName { get; set; } = "Cash";
    public string? DoctorName { get; set; }

    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal RoundOff { get; set; }
    public decimal NetAmount { get; set; }

    public PaymentMode PaymentMode { get; set; } = PaymentMode.Cash;
    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    /// <summary>
    /// Whether this was issued as a tax invoice. Recorded on the bill rather than
    /// read from settings, so a reprint years later shows what was actually given
    /// to the customer even if the clinic has registered since.
    ///
    /// Not the same as "carries tax" — a registered clinic selling only zero-rated
    /// goods still issues a tax invoice.
    /// </summary>
    public bool IsTaxInvoice { get; set; }

    public ICollection<SaleItem> Items { get; set; } = [];
}

public class SaleItem : BaseEntity
{
    public Guid SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Guid BatchId { get; set; }

    // Denormalised so a reprinted bill always shows what was actually sold,
    // even if the product or batch is later edited.
    public string ProductName { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public string HsnCode { get; set; } = string.Empty;

    /// <summary>Base units sold — 5 means five tablets, not five strips.</summary>
    public int Quantity { get; set; }

    /// <summary>Snapshotted so a reprint shows what the pack was at the time.</summary>
    public int UnitsPerPack { get; set; } = 1;

    public decimal Mrp { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal GstRate { get; set; }

    public decimal TaxableAmount { get; set; }
    public decimal GstAmount { get; set; }
    public decimal LineTotal { get; set; }

    /// <summary>"2 × 10 TAB + 5" for the printed bill.</summary>
    public string QuantityDescription => PackMath.Describe(Quantity, UnitsPerPack, PackLabel);

    public string? PackLabel { get; set; }
}

// ── Shared ─────────────────────────────────────────────────────────────────

/// <summary>Shop identity and print details. In the database, not appsettings,
/// so a second branch is a data change rather than a redeploy.</summary>
public class Setting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>Sequential document numbers. Tax invoice numbers must not have gaps.</summary>
public class Counter : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public int LastNumber { get; set; }
}

/// <summary>
/// A correction to what the shelf holds, and why.
///
/// Stock only ever moves by receiving or selling, both of which leave a document.
/// A manual correction has no document, so it writes one — otherwise a shortfall
/// is indistinguishable from theft, and nobody can answer what happened.
/// </summary>
public class StockAdjustment : BaseEntity
{
    public DateTime AdjustedOn { get; set; } = DateTime.Now;

    public Guid BatchId { get; set; }
    public Batch Batch { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    // Recorded rather than derived, so the trail survives later changes.
    public string ProductName { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;

    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }

    public AdjustmentReason Reason { get; set; } = AdjustmentReason.Recount;
    public string? Notes { get; set; }
    public string? AdjustedBy { get; set; }

    public int Change => QuantityAfter - QuantityBefore;
    public string Direction => Change >= 0 ? $"+{Change}" : Change.ToString();
}

/// <summary>Schedule H1 sales register — statutory, retained three years.</summary>
public class H1RegisterEntry : BaseEntity
{
    public DateTime SoldOn { get; set; }
    public string BillNo { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string? DoctorName { get; set; }
}
