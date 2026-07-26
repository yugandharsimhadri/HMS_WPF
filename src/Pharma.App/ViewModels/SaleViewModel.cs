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

    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private decimal _mrp;
    [ObservableProperty] private decimal _discountPercent;

    public string Expiry => ExpiryDate.ToString("MM/yy");
    public decimal Amount => GstCalculator.Line(Mrp, Quantity, DiscountPercent, GstRate).Net;

    partial void OnQuantityChanged(int value) => OnPropertyChanged(nameof(Amount));
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
    public ObservableCollection<Batch> Batches { get; } = [];
    public ObservableCollection<Visit> PrescribedVisits { get; } = [];

    // Step 1 — find the medicine
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private Batch? _selectedBatch;

    // Step 2 — quantity
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private decimal _discountPercent;

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

    public async Task LoadAsync()
    {
        await FindAsync();

        PrescribedVisits.Clear();
        foreach (var v in await opd.GetVisitsAsync(DateTime.Today))
        {
            if (v.Status is VisitStatus.Completed or VisitStatus.InConsultation)
                PrescribedVisits.Add(v);
        }

        Recalculate();
    }

    partial void OnSelectedProductChanged(Product? value)
        => LoadBatchesAsync(value).Forget("Loading batches for the counter");

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

    private async Task LoadBatchesAsync(Product? product)
    {
        Batches.Clear();
        SelectedBatch = null;
        if (product is null) return;

        foreach (var b in await pharmacy.GetSellableBatchesAsync(product.Id)) Batches.Add(b);

        // Nearest expiry first — dispensing order, and the default the counter wants.
        SelectedBatch = Batches.FirstOrDefault();
    }

    [RelayCommand]
    private void AddLine()
    {
        if (SelectedProduct is null || SelectedBatch is null)
        {
            Warn("Pick a medicine that has stock before adding it to the bill.");
            return;
        }

        if (Quantity <= 0)
        {
            Warn("Quantity must be at least 1.");
            return;
        }

        var alreadyOnBill = Lines.Where(l => l.BatchId == SelectedBatch.Id).Sum(l => l.Quantity);
        if (alreadyOnBill + Quantity > SelectedBatch.QtyOnHand)
        {
            Warn($"Only {SelectedBatch.QtyOnHand - alreadyOnBill} left of {SelectedProduct.Name} in batch {SelectedBatch.BatchNo}.");
            return;
        }

        if (SelectedBatch.IsExpired)
        {
            Warn($"Batch {SelectedBatch.BatchNo} expired on {SelectedBatch.ExpiryDate:dd MMM yyyy} and cannot be sold.");
            return;
        }

        var row = new SaleRow
        {
            ProductId = SelectedProduct.Id,
            BatchId = SelectedBatch.Id,
            ProductName = SelectedProduct.Name,
            BatchNo = SelectedBatch.BatchNo,
            ExpiryDate = SelectedBatch.ExpiryDate,
            HsnCode = SelectedProduct.HsnCode,
            GstRate = SelectedProduct.GstRate,
            Schedule = SelectedProduct.Schedule,
            Available = SelectedBatch.QtyOnHand,
            Quantity = Quantity,
            Mrp = SelectedBatch.Mrp,
            DiscountPercent = DiscountPercent
        };

        row.PropertyChanged += (_, _) => Recalculate();
        Lines.Add(row);
        Recalculate();

        if (SelectedProduct.Schedule is DrugSchedule.H1)
            Status = $"{SelectedProduct.Name} is Schedule H1 — record the prescriber's name on this bill.";

        // Reset for the next line, keeping the cursor flow moving.
        Search = "";
        Quantity = 1;
        DiscountPercent = 0;
        SelectedProduct = null;
        Matches.Clear();
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
                GstRate = product.GstRate,
                Schedule = product.Schedule,
                Available = batch.QtyOnHand,
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
            PaymentMode = PaymentMode
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
                if (full is not null) BillPrinter.Print(full, await settings.GetAsync());
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
        Batches.Clear();
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
