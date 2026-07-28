using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>One line on the bill being built at the counter.</summary>
public partial class SaleRow : ObservableObject
{
    public Guid ProductId { get; init; }
    public Guid BatchId { get; init; }
    public string ProductName { get; init; } = "";
    public string BatchNo { get; init; } = "";
    public DateTime ExpiryDate { get; init; }
    public string HsnCode { get; init; } = "3004";
    public decimal GstRate { get; init; }
    public DrugSchedule Schedule { get; init; }
    public int Available { get; init; }
    public int UnitsPerPack { get; init; } = 1;
    public string? PackLabel { get; init; }

    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private decimal _mrp;
    [ObservableProperty] private decimal _discountPercent;

    public string Expiry => ExpiryDate.ToString("MM'/'yy");

    /// <summary>"2 × 10 TAB + 3" so the operator can see what is being handed over.</summary>
    public string Packs => PackMath.Describe(Quantity, UnitsPerPack, PackLabel, UnitName);

    /// <summary>What one of them is called, so "9 loose" reads "9 tablets".</summary>
    public string? UnitName { get; init; }

    public decimal Amount => GstCalculator.Line(Mrp, UnitsPerPack, Quantity, DiscountPercent, GstRate).Net;

    partial void OnQuantityChanged(int value)
    {
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(Packs));
    }

    partial void OnMrpChanged(decimal value) => OnPropertyChanged(nameof(Amount));
    partial void OnDiscountPercentChanged(decimal value) => OnPropertyChanged(nameof(Amount));
}

/// <summary>
/// The pharmacy counter. Three steps per line: find the medicine, set the
/// quantity, add. Then choose payment and save.
/// </summary>
public partial class SaleViewModel(PharmacyService pharmacy, OpdService opd, SettingsService settings)
    : ObservableObject, IPage
{
    public string Title => "Pharmacy counter";
    public string Subtitle => Lines.Count == 0 ? "No items on this bill" : $"{Lines.Count} item(s) · ₹{Net:0.00}";

    public ObservableCollection<SaleRow> Lines { get; } = [];
    public ObservableCollection<Product> Matches { get; } = [];
    public ObservableCollection<Visit> PrescribedVisits { get; } = [];

    // Step 1 — find the medicine. Filters as it is typed; no button to press.
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private Product? _selectedProduct;

    // Step 2 — how many, and of what. The unit sits beside the number because
    // "9" on its own is what turned nine tablets into nine strips.
    [ObservableProperty] private int _quantity = 1;

    /// <summary>What the number in the quantity box counts.</summary>
    public ObservableCollection<string> QuantityUnits { get; } = [];

    [ObservableProperty] private string _quantityUnit = "";

    /// <summary>
    /// What was last chosen for each medicine, so the second sale of the day does
    /// not have to be told again. Kept for the session; the first time a medicine
    /// is picked the unit comes from what it is — tablets for a strip, bottles
    /// for a syrup.
    /// </summary>
    private readonly Dictionary<Guid, string> _rememberedUnit = [];

    /// <summary>No discounts are given at the counter, so every line is at MRP.</summary>
    private const decimal NoDiscount = 0m;

    /// <summary>What is in stock and what a unit costs, for the chosen medicine.</summary>
    [ObservableProperty] private string _selectedSummary = "";

    // Bill header
    [ObservableProperty] private string _customerName = "Cash";
    [ObservableProperty] private string _doctorName = "";
    [ObservableProperty] private PaymentMode _paymentMode = PaymentMode.Cash;
    [ObservableProperty] private Visit? _selectedVisit;
    [ObservableProperty] private string _status = "";

    // Totals
    [ObservableProperty] private decimal _gross;
    [ObservableProperty] private decimal _discount;
    [ObservableProperty] private decimal _taxable;
    [ObservableProperty] private decimal _cgst;
    [ObservableProperty] private decimal _sgst;
    [ObservableProperty] private decimal _roundOff;
    [ObservableProperty] private decimal _net;

    public Array PaymentModes => Enum.GetValues<PaymentMode>();

    /// <summary>Read on entry; an unregistered clinic charges no tax at all.</summary>
    private bool _gstRegistered;

    public async Task LoadAsync()
    {
        _gstRegistered = (await settings.GetAsync()).GstRegistered;

        await FindAsync();

        PrescribedVisits.Clear();
        foreach (var v in await opd.GetVisitsAsync(DateTime.Today))
        {
            if (v.Status is VisitStatus.Completed or VisitStatus.InConsultation)
                PrescribedVisits.Add(v);
        }

        Recalculate();
    }

    /// <summary>Filters as the operator types — two letters is enough.</summary>
    partial void OnSearchChanged(string value) => FindAsync().Forget("Searching medicines");

    partial void OnSelectedProductChanged(Product? value)
    {
        UpdateSelectedSummary();
        UpdateQuantityUnits(value);
    }

    partial void OnQuantityUnitChanged(string value)
    {
        if (SelectedProduct is { } product && !string.IsNullOrEmpty(value))
            _rememberedUnit[product.Id] = value;
    }

    /// <summary>
    /// Offers the units this medicine can actually be sold in. A syrup is bottles
    /// and nothing else; a strip of ten is tablets, or strips of 10.
    /// </summary>
    private void UpdateQuantityUnits(Product? product)
    {
        QuantityUnits.Clear();

        if (product is null)
        {
            QuantityUnit = "";
            return;
        }

        var perPack = Math.Max(1, product.UnitsPerPack);
        var single = product.DispensingUnit.Name(2);

        QuantityUnits.Add(single);

        // Only worth offering packs when a pack holds more than one.
        if (perPack > 1) QuantityUnits.Add($"{PackWord(product)} of {perPack}");

        QuantityUnit = _rememberedUnit.TryGetValue(product.Id, out var remembered)
                       && QuantityUnits.Contains(remembered)
            ? remembered
            : QuantityUnits[0];
    }

    private static string PackWord(Product product) => product.DispensingUnit switch
    {
        DispensingUnit.Tablet or DispensingUnit.Capsule => "strips",
        DispensingUnit.Sachet => "boxes",
        _ => "packs"
    };

    /// <summary>The quantity box in base units, whatever unit was chosen beside it.</summary>
    private int QuantityInUnits(Product product)
    {
        var perPack = Math.Max(1, product.UnitsPerPack);
        var choseWholePacks = perPack > 1 && QuantityUnit == $"{PackWord(product)} of {perPack}";

        return choseWholePacks ? Quantity * perPack : Quantity;
    }

    private void UpdateSelectedSummary()
    {
        if (SelectedProduct is null)
        {
            SelectedSummary = "";
            return;
        }

        var stock = SelectedProduct.StockOnHand;
        var unit = SelectedProduct.DispensingUnit.Name(stock);

        SelectedSummary = stock > 0
            ? $"{SelectedProduct.Name} · {stock} {unit} in stock · {UnitPriceOf(SelectedProduct):₹0.00} each"
            : $"{SelectedProduct.Name} · out of stock";
    }

    /// <summary>Cheapest-to-read price: what one unit costs from the batch that
    /// would actually be dispensed.</summary>
    private static decimal UnitPriceOf(Product product)
    {
        var next = product.Batches
            .Where(b => !b.IsDeleted && b.QtyOnHand > 0)
            .OrderBy(b => b.ExpiryDate)
            .FirstOrDefault();

        return next is null ? 0m : next.UnitPrice;
    }

    partial void OnSelectedVisitChanged(Visit? value)
    {
        if (value is null) return;
        CustomerName = value.Patient.Name;
        DoctorName = value.Doctor.Name;
    }

    [RelayCommand]
    private async Task FindAsync()
    {
        Matches.Clear();
        foreach (var p in await pharmacy.SearchProductsAsync(Search, 40)) Matches.Add(p);

        // A single hit is almost always the one wanted — select it so the operator
        // can go straight to the quantity box.
        if (Matches.Count == 1) SelectedProduct = Matches[0];
    }

    /// <summary>Set while lines are being rebuilt, so edits do not chase their own tail.</summary>
    private bool _relaying;

    private void Watch(SaleRow row) => row.PropertyChanged += OnLineChanged;

    /// <summary>
    /// Quantity is the one thing that can be changed on a bill line, and changing
    /// it has to re-take the stock: raising it past what its batch holds needs
    /// another batch, and lowering it should give the rest back. Without this the
    /// line stays pinned to one batch and only fails when the bill is saved.
    /// </summary>
    private void OnLineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Recalculate();

        if (_relaying || e.PropertyName != nameof(SaleRow.Quantity)) return;
        if (sender is not SaleRow row) return;

        ReallocateAsync(row.ProductId).Forget("Re-taking stock for an edited line");
    }

    private async Task ReallocateAsync(Guid productId)
    {
        var wanted = Lines.Where(l => l.ProductId == productId).Sum(l => l.Quantity);
        if (wanted <= 0) return;

        var product = Matches.FirstOrDefault(p => p.Id == productId)
                      ?? (await pharmacy.SearchProductsAsync(null, 1000)).FirstOrDefault(p => p.Id == productId);

        if (product is null) return;

        using var log = AppLog.Enter("Counter.ReallocateLine", $"product={productId} wanted={wanted}");

        var (allocations, shortfall) = await pharmacy.AllocateAsync(productId, wanted);

        if (shortfall > 0)
        {
            var have = allocations.Sum(a => a.Units);
            log.Skip($"short by {shortfall}; only {have} sellable");

            Warn($"Only {have} {product.DispensingUnit.Name(Math.Max(have, 2))} of " +
                 $"{product.Name} can be sold. The line has been set back to {have}.");
        }

        Relay(product, allocations);
        log.Ok($"{allocations.Count} line(s)");
    }

    /// <summary>Replaces every line for one medicine from a fresh allocation.</summary>
    private void Relay(Product product, IReadOnlyList<PharmacyService.Allocation> allocations)
    {
        _relaying = true;

        try
        {
            foreach (var existing in Lines.Where(l => l.ProductId == product.Id).ToList())
            {
                existing.PropertyChanged -= OnLineChanged;
                Lines.Remove(existing);
            }

            foreach (var allocation in allocations)
            {
                var row = new SaleRow
                {
                    ProductId = product.Id,
                    BatchId = allocation.Batch.Id,
                    ProductName = product.Name,
                    BatchNo = allocation.Batch.BatchNo,
                    ExpiryDate = allocation.Batch.ExpiryDate,
                    HsnCode = product.HsnCode,
                    GstRate = _gstRegistered ? product.GstRate : 0m,
                    Schedule = product.Schedule,
                    Available = allocation.Batch.QtyOnHand,
                    UnitsPerPack = allocation.Batch.UnitsPerPack,
                    PackLabel = product.PackSize,
                    UnitName = product.DispensingUnit.Name(2),
                    Quantity = allocation.Units,
                    Mrp = allocation.Batch.Mrp,
                    DiscountPercent = NoDiscount
                };

                Watch(row);
                Lines.Add(row);
            }
        }
        finally
        {
            _relaying = false;
        }

        Recalculate();
    }

    // Batch selection is gone from the counter: nearest expiry is chosen for the
    // operator, and a quantity larger than one batch simply spans several. The
    // batch still reaches the bill, because it has to be printed.

    /// <summary>
    /// Adds the requested quantity, taken from whichever batches fill it — nearest
    /// expiry first. The operator never picks a batch; asking for 20 when the
    /// oldest holds 15 quietly becomes two lines rather than an error.
    /// </summary>
    [RelayCommand]
    private async Task AddLineAsync()
    {
        using var log = AppLog.Enter(
            "Counter.AddLine",
            $"product='{SelectedProduct?.Name}' id={SelectedProduct?.Id} qty={Quantity} unit='{QuantityUnit}'");

        if (SelectedProduct is null)
        {
            log.Skip("no medicine chosen");
            Warn("Choose a medicine from the list first.");
            return;
        }

        if (Quantity <= 0)
        {
            log.Skip($"quantity {Quantity} is not sellable");
            Warn("Quantity must be at least 1.");
            return;
        }

        var product = SelectedProduct;

        // Whatever the operator typed, the bill is built in base units.
        var adding = QuantityInUnits(product);

        // Some things cannot be broken open — a sealed bottle, a sachet strip
        // the manufacturer seals as one. The checkbox on the medicine says so
        // and this is where it has to mean something.
        if (!product.AllowLooseSale && product.UnitsPerPack > 1 && adding % product.UnitsPerPack != 0)
        {
            var packs = (adding / product.UnitsPerPack) + 1;

            log.Skip($"loose sale refused: {adding} is not a multiple of {product.UnitsPerPack}");

            Warn($"{product.Name} is not sold loose — it goes out in whole packs of " +
                 $"{product.UnitsPerPack}. Enter {packs * product.UnitsPerPack} for {packs} pack(s).");
            return;
        }

        // Whatever is already on this bill is committed as far as stock goes.
        var alreadyOnBill = Lines.Where(l => l.ProductId == product.Id).Sum(l => l.Quantity);

        var (allocations, shortfall) = await pharmacy.AllocateAsync(product.Id, alreadyOnBill + adding);

        if (shortfall > 0)
        {
            var have = allocations.Sum(a => a.Units) - alreadyOnBill;
            var unit = product.DispensingUnit.Name(Math.Max(have, 2));

            log.Skip($"short by {shortfall}; only {have} sellable");

            Warn(have <= 0
                ? $"{product.Name} has none left that can be sold."
                : $"Only {have} {unit} of {product.Name} left to sell.");
            return;
        }

        // Re-lay the lines for this medicine from the fresh allocation, so a second
        // Add of the same medicine does not double-count against the same batch.
        foreach (var existing in Lines.Where(l => l.ProductId == product.Id).ToList())
            Lines.Remove(existing);

        foreach (var allocation in allocations)
        {
            var row = new SaleRow
            {
                ProductId = product.Id,
                BatchId = allocation.Batch.Id,
                ProductName = product.Name,
                BatchNo = allocation.Batch.BatchNo,
                ExpiryDate = allocation.Batch.ExpiryDate,
                HsnCode = product.HsnCode,
                // Zero when the clinic is not registered, so no tax is extracted
                // from the MRP and the bill carries none.
                GstRate = _gstRegistered ? product.GstRate : 0m,
                Schedule = product.Schedule,
                Available = allocation.Batch.QtyOnHand,
                UnitsPerPack = allocation.Batch.UnitsPerPack,
                PackLabel = product.PackSize,
                UnitName = product.DispensingUnit.Name(2),
                Quantity = allocation.Units,
                Mrp = allocation.Batch.Mrp,
                DiscountPercent = NoDiscount
            };

            Watch(row);
            Lines.Add(row);
        }

        Recalculate();

        // Nearest expiry goes first, which is right — but handing over something
        // with a fortnight left without saying so is how a parent comes back.
        var soonest = allocations.Min(a => a.Batch.DaysToExpiry);

        if (soonest <= 30)
        {
            var batch = allocations.First(a => a.Batch.DaysToExpiry == soonest).Batch;

            Warn($"{product.Name} batch {batch.BatchNo} expires {batch.ExpiryDate:MMM yyyy} — " +
                 $"{soonest} day(s) away. It is still on the bill; tell the customer.");
        }

        // Two prices for one medicine on a bill looks like a mistake unless the
        // operator knows before the customer asks.
        if (allocations.Count > 1)
        {
            var prices = allocations.Select(a => a.Batch.Mrp).Distinct().Count();

            AppLog.Info(
                $"Counter: {product.Name} split across {allocations.Count} batches " +
                $"[{string.Join(", ", allocations.Select(a => $"{a.Batch.BatchNo}×{a.Units}"))}]" +
                (prices > 1 ? " at different prices." : "."));
        }

        Status = product.Schedule is DrugSchedule.H1
            ? $"{product.Name} is Schedule H1 — record the prescriber's name on this bill."
            : allocations.Count > 1
                ? $"{product.Name}: {adding} from {allocations.Count} batches — " +
                  $"{string.Join(", ", allocations.Select(a => $"{a.Units} from {a.Batch.BatchNo}"))}" +
                  (allocations.Select(a => a.Batch.Mrp).Distinct().Count() > 1
                      ? " at different prices."
                      : ".")
                : $"{product.Name} added.";

        log.Ok($"{Lines.Count} line(s) on the bill, net {Net:0.00}");

        // The line is on the bill, so the search that found it goes. Left there,
        // the previous medicine stays typed and highlighted while the operator
        // starts on the next one — and pressing Add again re-lays the line they
        // already have instead of adding the new medicine.
        Quantity = 1;
        Search = "";
        await FindAsync();
        SelectedProduct = null;
    }

    [RelayCommand]
    private void RemoveLine(SaleRow? row)
    {
        if (row is null) return;
        Lines.Remove(row);
        Recalculate();
    }

    /// <summary>
    /// Puts stock on the shelf for the selected medicine without leaving the
    /// bill. The shop knows the medicine is there; the system does not, and
    /// sending the operator away to do a full goods-inward with a patient
    /// waiting is how a counter stops being used.
    /// </summary>
    [RelayCommand]
    private async Task QuickStockAsync()
    {
        if (SelectedProduct is null)
        {
            Warn("Choose the medicine you are adding stock for.");
            return;
        }

        await Safely.RunAsync(async () =>
        {
            var window = new Views.QuickStockWindow(SelectedProduct)
            {
                Owner = Application.Current.MainWindow
            };

            window.ShowDialog();
            if (!window.Added) return;

            var id = SelectedProduct.Id;

            await FindAsync();
            SelectedProduct = Matches.FirstOrDefault(p => p.Id == id);
            UpdateSelectedSummary();

            Status = $"{SelectedProduct?.Name} is now on the shelf. " +
                     $"It is listed under Reports → Stock to reconcile until the supplier bill arrives.";
        }, "Adding stock at the counter", m => Status = m);
    }

    [RelayCommand]
    private async Task LoadPrescriptionAsync()
    {
        using var log = AppLog.Enter("Counter.LoadPrescription", $"visit={SelectedVisit?.Id}");

        if (SelectedVisit is null)
        {
            log.Skip("no visit chosen");
            Warn("Choose a patient from today's OPD list first.");
            return;
        }

        var visit = await opd.GetVisitAsync(SelectedVisit.Id);

        if (visit is null)
        {
            log.Skip("visit no longer exists");
            return;
        }

        var missing = new List<string>();
        var partial = new List<string>();

        foreach (var item in visit.Prescription)
        {
            Product? product = null;

            if (item.ProductId is { } id)
                product = (await pharmacy.SearchProductsAsync(null, 1000)).FirstOrDefault(p => p.Id == id);

            product ??= (await pharmacy.SearchProductsAsync(item.MedicineName, 5)).FirstOrDefault();

            if (product is null)
            {
                missing.Add(item.MedicineName);
                continue;
            }

            var wanted = Math.Max(1, item.Quantity);
            var (allocations, shortfall) = await pharmacy.AllocateAsync(product.Id, wanted);

            if (allocations.Count == 0)
            {
                missing.Add($"{item.MedicineName} (no stock)");
                continue;
            }

            // Say so rather than quietly billing less than the doctor wrote.
            if (shortfall > 0)
                partial.Add($"{product.Name} ({wanted - shortfall} of {wanted})");

            // Loading twice is normal — the operator loads, finds something is
            // out of stock, receives it, and loads again. Re-lay this medicine's
            // lines instead of adding a second copy of it to the bill.
            foreach (var existing in Lines.Where(l => l.ProductId == product.Id).ToList())
                Lines.Remove(existing);

            // A course can span batches; each batch is its own line because the
            // batch number handed over has to appear on the invoice.
            foreach (var allocation in allocations)
            {
                var row = new SaleRow
                {
                    ProductId = product.Id,
                    BatchId = allocation.Batch.Id,
                    ProductName = product.Name,
                    BatchNo = allocation.Batch.BatchNo,
                    ExpiryDate = allocation.Batch.ExpiryDate,
                    HsnCode = product.HsnCode,
                    GstRate = _gstRegistered ? product.GstRate : 0m,
                    Schedule = product.Schedule,
                    Available = allocation.Batch.QtyOnHand,
                    UnitsPerPack = allocation.Batch.UnitsPerPack,
                    PackLabel = product.PackSize,
                    UnitName = product.DispensingUnit.Name(2),
                    Quantity = allocation.Units,
                    Mrp = allocation.Batch.Mrp
                };

                row.PropertyChanged += (_, _) => Recalculate();
                Lines.Add(row);
            }
        }

        Recalculate();

        var notes = new List<string>();
        if (missing.Count > 0) notes.Add($"Not added: {string.Join(", ", missing)}");
        if (partial.Count > 0) notes.Add($"Short: {string.Join(", ", partial)}");

        Status = notes.Count == 0
            ? $"Loaded {visit.Prescription.Count} item(s) from token {visit.TokenNo}."
            : $"Loaded. {string.Join(". ", notes)}.";

        log.Ok($"{Lines.Count} line(s) added; missing={missing.Count} short={partial.Count}");
    }

    [RelayCommand]
    private Task SaveAsync() => CompleteSaleAsync(print: false);

    [RelayCommand]
    private Task SaveAndPrintAsync() => CompleteSaleAsync(print: true);

    private async Task CompleteSaleAsync(bool print)
    {
        using var log = AppLog.Enter(
            "Counter.SaveBill",
            $"print={print} lines={Lines.Count} customer='{CustomerName}' pay={PaymentMode}");

        if (Lines.Count == 0)
        {
            log.Skip("nothing on the bill");
            Warn("Add at least one medicine to the bill.");
            return;
        }

        // The H1 register is a statutory record kept for three years, and the
        // prescriber is the whole point of it. Recording the sale without one
        // leaves a hole in a book an inspector can ask to see.
        if (Lines.Any(l => l.Schedule == DrugSchedule.H1) && string.IsNullOrWhiteSpace(DoctorName))
        {
            var h1 = string.Join(", ", Lines.Where(l => l.Schedule == DrugSchedule.H1)
                                            .Select(l => l.ProductName).Distinct());

            log.Skip($"H1 refused: no prescriber for {h1}");

            Warn($"{h1} is Schedule H1. Enter the prescribing doctor's name before saving — " +
                 $"it goes in the H1 register, which has to be kept for three years.");
            return;
        }

        var sale = new Sale
        {
            BillDate = DateTime.Now,
            PatientId = SelectedVisit?.PatientId,
            VisitId = SelectedVisit?.Id,
            CustomerName = string.IsNullOrWhiteSpace(CustomerName) ? "Cash" : CustomerName.Trim(),
            DoctorName = string.IsNullOrWhiteSpace(DoctorName) ? null : DoctorName.Trim(),
            PaymentMode = PaymentMode,
            IsTaxInvoice = _gstRegistered
        };

        var lines = Lines.Select(l => new SaleLine
        {
            ProductId = l.ProductId,
            BatchId = l.BatchId,
            ProductName = l.ProductName,
            BatchNo = l.BatchNo,
            ExpiryDate = l.ExpiryDate,
            HsnCode = l.HsnCode,
            Quantity = l.Quantity,
            UnitsPerPack = l.UnitsPerPack,
            PackLabel = l.PackLabel,
            Mrp = l.Mrp,
            DiscountPercent = l.DiscountPercent,
            GstRate = l.GstRate,
            Schedule = l.Schedule
        }).ToList();

        try
        {
            var saved = await pharmacy.SaveSaleAsync(sale, lines);
            Status = $"Bill {saved.BillNo} saved · ₹{saved.NetAmount:0.00}";

            if (print)
            {
                var full = await pharmacy.GetSaleAsync(saved.Id);
                if (full is not null)
                {
                    var shop = await settings.GetAsync();
                    PrintService.Preview(() => BillPrinter.Build(full, shop), $"Bill {full.BillNo}");
                }
            }

            NewBill();
            log.Ok($"{saved.BillNo} net={saved.NetAmount:0.00} printed={print}");
        }
        catch (Exception ex)
        {
            // The counter shows the plain message; the log keeps the whole thing,
            // because this is the one place money stops being recorded.
            log.Skip($"refused: {ex.GetType().Name}: {ex.Message}");
            AppLog.Error("Saving the bill failed.", ex);

            Warn(ex.Message);
        }
    }

    [RelayCommand]
    private void NewBill()
    {
        Lines.Clear();
        Search = "";
        Quantity = 1;
        CustomerName = "Cash";
        DoctorName = "";
        SelectedProduct = null;
        SelectedVisit = null;
        Matches.Clear();

        Recalculate();
    }

    private void Recalculate()
    {
        // UnitsPerPack has to be passed: without it every tablet prices at the
        // full strip MRP, and the total on screen stops matching the bill that
        // gets saved and printed.
        var amounts = GstCalculator.Bill(
            Lines.Select(l => GstCalculator.Line(l.Mrp, l.UnitsPerPack, l.Quantity, l.DiscountPercent, l.GstRate)));

        Gross = amounts.Gross;
        Discount = amounts.Discount;
        Taxable = amounts.Taxable;
        Cgst = amounts.Cgst;
        Sgst = amounts.Sgst;
        RoundOff = amounts.RoundOff;
        Net = amounts.Net;

        OnPropertyChanged(nameof(Subtitle));
    }

    private void Warn(string message)
    {
        Status = message;
        MessageBox.Show(message, "Pharmacy counter", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
