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
    public string Packs => PackMath.Describe(Quantity, UnitsPerPack, PackLabel);

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

    // Step 2 — quantity
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private decimal _discountPercent;

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

    partial void OnSelectedProductChanged(Product? value) => UpdateSelectedSummary();

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
        if (SelectedProduct is null)
        {
            Warn("Choose a medicine from the list first.");
            return;
        }

        if (Quantity <= 0)
        {
            Warn("Quantity must be at least 1.");
            return;
        }

        var product = SelectedProduct;

        // Whatever is already on this bill is committed as far as stock goes.
        var alreadyOnBill = Lines.Where(l => l.ProductId == product.Id).Sum(l => l.Quantity);

        var (allocations, shortfall) = await pharmacy.AllocateAsync(product.Id, alreadyOnBill + Quantity);

        if (shortfall > 0)
        {
            var have = allocations.Sum(a => a.Units) - alreadyOnBill;
            var unit = product.DispensingUnit.Name(Math.Max(have, 2));

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
                Quantity = allocation.Units,
                Mrp = allocation.Batch.Mrp,
                DiscountPercent = DiscountPercent
            };

            row.PropertyChanged += (_, _) => Recalculate();
            Lines.Add(row);
        }

        Recalculate();

        Status = product.Schedule is DrugSchedule.H1
            ? $"{product.Name} is Schedule H1 — record the prescriber's name on this bill."
            : allocations.Count > 1
                ? $"{product.Name} added from {allocations.Count} batches."
                : $"{product.Name} added.";

        // The search and its results stay put, so the next medicine is one click
        // away and the operator can see what else is on the shelf.
        Quantity = 1;
        DiscountPercent = 0;
    }

    [RelayCommand]
    private void RemoveLine(SaleRow? row)
    {
        if (row is null) return;
        Lines.Remove(row);
        Recalculate();
    }

    [RelayCommand]
    private async Task LoadPrescriptionAsync()
    {
        if (SelectedVisit is null)
        {
            Warn("Choose a patient from today's OPD list first.");
            return;
        }

        var visit = await opd.GetVisitAsync(SelectedVisit.Id);
        if (visit is null) return;

        var missing = new List<string>();

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

            var batch = (await pharmacy.GetSellableBatchesAsync(product.Id)).FirstOrDefault();
            if (batch is null)
            {
                missing.Add($"{item.MedicineName} (no stock)");
                continue;
            }

            var row = new SaleRow
            {
                ProductId = product.Id,
                BatchId = batch.Id,
                ProductName = product.Name,
                BatchNo = batch.BatchNo,
                ExpiryDate = batch.ExpiryDate,
                HsnCode = product.HsnCode,
                GstRate = _gstRegistered ? product.GstRate : 0m,
                Schedule = product.Schedule,
                Available = batch.QtyOnHand,
                UnitsPerPack = batch.UnitsPerPack,
                PackLabel = product.PackSize,
                Quantity = Math.Max(1, Math.Min(item.Quantity, batch.QtyOnHand)),
                Mrp = batch.Mrp
            };

            row.PropertyChanged += (_, _) => Recalculate();
            Lines.Add(row);
        }

        Recalculate();
        Status = missing.Count == 0
            ? $"Loaded {visit.Prescription.Count} item(s) from token {visit.TokenNo}."
            : $"Loaded. Not added: {string.Join(", ", missing)}.";
    }

    [RelayCommand]
    private Task SaveAsync() => CompleteSaleAsync(print: false);

    [RelayCommand]
    private Task SaveAndPrintAsync() => CompleteSaleAsync(print: true);

    private async Task CompleteSaleAsync(bool print)
    {
        if (Lines.Count == 0)
        {
            Warn("Add at least one medicine to the bill.");
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
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    [RelayCommand]
    private void NewBill()
    {
        Lines.Clear();
        Search = "";
        Quantity = 1;
        DiscountPercent = 0;
        CustomerName = "Cash";
        DoctorName = "";
        SelectedProduct = null;
        SelectedVisit = null;
        Matches.Clear();

        Recalculate();
    }

    private void Recalculate()
    {
        var amounts = GstCalculator.Bill(
            Lines.Select(l => GstCalculator.Line(l.Mrp, l.Quantity, l.DiscountPercent, l.GstRate)));

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
