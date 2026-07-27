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
    public DateTime? FollowUpOn { get; set; }

    public ICollection<PrescriptionItem> Prescription { get; set; } = [];
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
    public string Name { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    /// <summary>Printed pack, e.g. "10 TAB" or "100 ML". Free text — shops describe packs their own way.</summary>
    public string? PackSize { get; set; }
    public string HsnCode { get; set; } = "3004";
    public decimal GstRate { get; set; } = 12m;
    public DrugSchedule Schedule { get; set; } = DrugSchedule.None;
    public string? RackLocation { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Batch> Batches { get; set; } = [];

    public int StockOnHand => Batches.Where(b => !b.IsDeleted).Sum(b => b.QtyOnHand);

    /// <summary>How far under the reorder level current stock has fallen. Zero when not low.</summary>
    public int Shortage => Math.Max(0, ReorderLevel - StockOnHand);
}

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
    public decimal Mrp { get; set; }
    public decimal PurchaseRate { get; set; }
    public int QtyOnHand { get; set; }
    public string? SupplierName { get; set; }
    public DateTime ReceivedOn { get; set; } = DateTime.Today;

    public bool IsExpired => ExpiryDate.Date < DateTime.Today;
    public string Display => $"{BatchNo} · exp {ExpiryDate:MM/yy} · ₹{Mrp:0.00} · {QtyOnHand} left";
}

public class StockEntry : BaseEntity
{
    public string EntryNo { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; } = DateTime.Today;
    public string? SupplierName { get; set; }
    public string? SupplierInvoiceNo { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    public ICollection<StockEntryItem> Items { get; set; } = [];
}

public class StockEntryItem : BaseEntity
{
    public Guid StockEntryId { get; set; }
    public StockEntry StockEntry { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string BatchNo { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public int Quantity { get; set; }
    /// <summary>Scheme quantity (the "+1" in 10+1). Adds to stock, costs nothing.</summary>
    public int FreeQuantity { get; set; }
    public decimal PurchaseRate { get; set; }
    public decimal Mrp { get; set; }

    public decimal LineTotal => Quantity * PurchaseRate;
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

    public int Quantity { get; set; }
    public decimal Mrp { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal GstRate { get; set; }

    public decimal TaxableAmount { get; set; }
    public decimal GstAmount { get; set; }
    public decimal LineTotal { get; set; }
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
