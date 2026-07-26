using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// Stock: what is on the shelf, receiving more, and correcting a count.
///
/// Split from the medicine catalogue because they are different jobs done by
/// different people at different times — the catalogue is set up once, stock
/// moves every delivery.
/// </summary>
public partial class InventoryViewModel(PharmacyService pharmacy) : ObservableObject, IPage
{
    public string Title => "Inventory";
    public string Subtitle => SelectedProduct is null
        ? $"{Products.Count} medicine(s) · pick one to receive or correct stock"
        : $"{SelectedProduct.Name} · {SelectedProduct.StockOnHand} " +
          $"{SelectedProduct.DispensingUnit.Name(SelectedProduct.StockOnHand)} on hand";

    public ObservableCollection<Product> Products { get; } = [];
    public ObservableCollection<Batch> Batches { get; } = [];
    public ObservableCollection<StockAdjustment> Adjustments { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private string _status = "";

    // Receiving
    [ObservableProperty] private string _batchNo = "";
    [ObservableProperty] private DateTime _expiryDate = DateTime.Today.AddYears(2);
    [ObservableProperty] private int _packs;
    [ObservableProperty] private int _freePacks;
    [ObservableProperty] private decimal _purchaseRate;
    [ObservableProperty] private decimal _mrp;
    [ObservableProperty] private string _supplierName = "";
    [ObservableProperty] private string _supplierInvoiceNo = "";
    [ObservableProperty] private string _intakePreview = "";

    // Correcting
    [ObservableProperty] private Batch? _selectedBatch;
    [ObservableProperty] private int _correctedQuantity;
    [ObservableProperty] private AdjustmentReason _adjustmentReason = AdjustmentReason.Recount;
    [ObservableProperty] private string _adjustmentNotes = "";

    public Array AdjustmentReasons => Enum.GetValues<AdjustmentReason>();

    /// <summary>
    /// Says so when a medicine's pack size and its units-per-pack disagree.
    /// That combination sells whole strips to anyone asking for tablets and
    /// reports no error, so it has to be visible where stock is handled.
    /// </summary>
    [ObservableProperty] private string _packWarning = "";

    private void UpdatePackWarning()
    {
        PackWarning = "";
        if (SelectedProduct is not { } product) return;

        var stated = PackMath.UnitsFromPacking(product.PackSize);
        var perPack = Math.Max(1, product.UnitsPerPack);

        if (stated is { } n && n != perPack)
        {
            PackWarning =
                $"⚠ The pack size says {n} per pack but this medicine is set to {perPack}. " +
                $"The counter will sell whole packs to anyone asking for " +
                $"{product.DispensingUnit.Name(2)}. Fix it on the Medicines screen — " +
                $"set Units in one pack to {n} and save, and the stock already on the " +
                $"shelf is re-counted with it.";
            return;
        }

        // The medicine may be right while stock received earlier is not.
        var stale = Batches.Where(b => b.UnitsPerPack != perPack).ToList();

        if (stale.Count > 0)
            PackWarning =
                $"⚠ {stale.Count} batch(es) here were received at a different pack size. " +
                $"Open this medicine on the Medicines screen and save it to re-count them.";
    }

    public async Task LoadAsync()
    {
        await FindAsync();
        await LoadAdjustmentsAsync();
    }

    [RelayCommand]
    private async Task FindAsync()
    {
        var previous = SelectedProduct?.Id;

        Products.Clear();
        foreach (var p in await pharmacy.SearchProductsAsync(Search, 200)) Products.Add(p);

        SelectedProduct = Products.FirstOrDefault(p => p.Id == previous);
        OnPropertyChanged(nameof(Subtitle));
    }

    partial void OnSelectedProductChanged(Product? value)
    {
        OnPropertyChanged(nameof(Subtitle));

        // A price left over from the last medicine is how the wrong MRP ends up
        // on a batch, so the receiving form starts clean for each one.
        BatchNo = "";
        Packs = FreePacks = 0;
        PurchaseRate = Mrp = 0;
        ExpiryDate = DateTime.Today.AddYears(2);

        UpdateIntakePreview();

        Batches.Clear();
        UpdatePackWarning();

        if (value is null) return;

        LoadBatchesAsync(value.Id).Forget("Loading batches");
    }

    private async Task LoadBatchesAsync(Guid productId)
    {
        Batches.Clear();
        foreach (var b in await pharmacy.GetSellableBatchesAsync(productId)) Batches.Add(b);

        UpdatePackWarning();
    }

    private async Task LoadAdjustmentsAsync()
    {
        Adjustments.Clear();
        foreach (var a in await pharmacy.GetAdjustmentsAsync(100)) Adjustments.Add(a);
    }

    // ── Receiving ──────────────────────────────────────────────────────────

    partial void OnPacksChanged(int value) => UpdateIntakePreview();
    partial void OnFreePacksChanged(int value) => UpdateIntakePreview();

    /// <summary>
    /// Spells out packs in, units out. "Qty" alone is the single most misread
    /// field in a pharmacy: the shop counts strips, the counter sells tablets.
    /// </summary>
    private void UpdateIntakePreview()
    {
        var total = Packs + FreePacks;

        if (SelectedProduct is null || total <= 0)
        {
            IntakePreview = "";
            return;
        }

        var perPack = Math.Max(1, SelectedProduct.UnitsPerPack);
        var units = total * perPack;
        var unitName = SelectedProduct.DispensingUnit.Name(units);

        IntakePreview = perPack > 1
            ? $"{total} pack(s) × {perPack} = {units} {unitName} onto the shelf"
            : $"{units} {unitName} onto the shelf";
    }

    [RelayCommand]
    private async Task ReceiveStockAsync()
    {
        if (SelectedProduct is null)
        {
            Warn("Choose the medicine you are receiving.");
            return;
        }

        if (string.IsNullOrWhiteSpace(BatchNo))
        {
            Warn("Batch number is printed on the pack and has to appear on the bill.");
            return;
        }

        if (Packs <= 0 && FreePacks <= 0)
        {
            Warn("Enter how many packs arrived.");
            return;
        }

        if (Mrp <= 0)
        {
            Warn("Enter the MRP printed on the pack — the counter prices from it.");
            return;
        }

        if (ExpiryDate.Date <= DateTime.Today)
        {
            Warn("Expiry must be in the future.");
            return;
        }

        await Safely.RunAsync(async () =>
        {
            var entry = new StockEntry
            {
                EntryDate = DateTime.Today,
                SupplierName = Empty(SupplierName),
                SupplierInvoiceNo = Empty(SupplierInvoiceNo)
            };

            var item = new StockEntryItem
            {
                ProductId = SelectedProduct.Id,
                BatchNo = BatchNo.Trim(),
                ExpiryDate = ExpiryDate,
                Quantity = Packs,
                FreeQuantity = FreePacks,
                UnitsPerPack = SelectedProduct.UnitsPerPack,
                PurchaseRate = PurchaseRate,
                Mrp = Mrp
            };

            var saved = await pharmacy.ReceiveStockAsync(entry, [item]);

            Status = $"{saved.EntryNo}: {item.UnitsReceived} " +
                     $"{SelectedProduct.DispensingUnit.Name(item.UnitsReceived)} of {SelectedProduct.Name} " +
                     $"added to batch {item.BatchNo}.";

            BatchNo = "";
            Packs = FreePacks = 0;
            PurchaseRate = 0;

            await LoadBatchesAsync(SelectedProduct.Id);
            await FindAsync();
        }, "Receiving stock", m => Status = m);
    }

    [RelayCommand]
    private async Task ImportBillAsync()
    {
        await Safely.RunAsync(async () =>
        {
            var window = new Views.ImportWindow { Owner = Application.Current.MainWindow };
            window.ShowDialog();

            if (!window.Imported) return;

            await FindAsync();
            if (SelectedProduct is not null) await LoadBatchesAsync(SelectedProduct.Id);

            Status = "Stock imported. It was added to what was already on the shelf.";
        }, "Importing a supplier bill", m => Status = m);
    }

    // ── Correcting ─────────────────────────────────────────────────────────

    partial void OnSelectedBatchChanged(Batch? value) => CorrectedQuantity = value?.QtyOnHand ?? 0;

    [RelayCommand]
    private async Task CorrectStockAsync()
    {
        if (SelectedBatch is null)
        {
            Warn("Choose the batch whose count is wrong.");
            return;
        }

        await Safely.RunAsync(async () =>
        {
            var adjustment = await pharmacy.AdjustStockAsync(
                SelectedBatch.Id, CorrectedQuantity, AdjustmentReason, AdjustmentNotes);

            Status = $"{adjustment.ProductName} batch {adjustment.BatchNo}: " +
                     $"{adjustment.QuantityBefore} → {adjustment.QuantityAfter} ({adjustment.Reason}).";

            AdjustmentNotes = "";

            if (SelectedProduct is not null) await LoadBatchesAsync(SelectedProduct.Id);
            await LoadAdjustmentsAsync();
            await FindAsync();
        }, "Correcting the stock count", m => Status = m);
    }

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Warn(string message)
    {
        Status = message;
        MessageBox.Show(message, "Inventory", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
