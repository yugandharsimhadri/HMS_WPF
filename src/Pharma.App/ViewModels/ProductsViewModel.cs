using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// Medicine catalogue and stock intake. Adding a medicine and putting stock
/// against it are two small forms side by side, so a new drug goes from unknown
/// to sellable without leaving the screen.
/// </summary>
public partial class ProductsViewModel(PharmacyService pharmacy) : ObservableObject, IPage
{
    public string Title => "Medicines";
    public string Subtitle => $"{Products.Count} medicine(s) listed";

    public ObservableCollection<Product> Products { get; } = [];
    public ObservableCollection<Batch> Batches { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private Product? _selectedProduct;

    // Medicine form
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _manufacturer = "";
    [ObservableProperty] private string _packSize = "";
    [ObservableProperty] private string _hsnCode = "3004";
    [ObservableProperty] private decimal _gstRate = 12m;
    [ObservableProperty] private DrugSchedule _schedule = DrugSchedule.None;
    [ObservableProperty] private string _rackLocation = "";
    [ObservableProperty] private int _reorderLevel;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private int _unitsPerPack = 1;
    [ObservableProperty] private bool _allowLooseSale = true;

    // Stock intake form
    [ObservableProperty] private string _batchNo = "";
    [ObservableProperty] private DateTime _expiryDate = DateTime.Today.AddYears(2);
    [ObservableProperty] private int _quantity;
    [ObservableProperty] private int _freeQuantity;
    [ObservableProperty] private decimal _purchaseRate;
    [ObservableProperty] private decimal _mrp;
    [ObservableProperty] private string _supplierName = "";
    [ObservableProperty] private string _supplierInvoiceNo = "";

    [ObservableProperty] private string _status = "";

    // Correcting a count
    [ObservableProperty] private Batch? _selectedBatch;
    [ObservableProperty] private int _correctedQuantity;
    [ObservableProperty] private AdjustmentReason _adjustmentReason = AdjustmentReason.Recount;
    [ObservableProperty] private string _adjustmentNotes = "";

    public ObservableCollection<StockAdjustment> Adjustments { get; } = [];
    public Array AdjustmentReasons => Enum.GetValues<AdjustmentReason>();

    partial void OnSelectedBatchChanged(Batch? value)
        => CorrectedQuantity = value?.QtyOnHand ?? 0;

    public Array Schedules => Enum.GetValues<DrugSchedule>();
    public bool HasProduct => SelectedProduct is not null;

    public async Task LoadAsync()
    {
        await FindAsync();
        await LoadAdjustmentsAsync();
    }

    [RelayCommand]
    private async Task FindAsync()
    {
        var selectedId = SelectedProduct?.Id;

        Products.Clear();
        foreach (var p in await pharmacy.SearchProductsAsync(Search, 200)) Products.Add(p);

        SelectedProduct = Products.FirstOrDefault(p => p.Id == selectedId);
        OnPropertyChanged(nameof(Subtitle));
    }

    partial void OnSelectedProductChanged(Product? value)
    {
        OnPropertyChanged(nameof(HasProduct));

        if (value is null)
        {
            Batches.Clear();
            return;
        }

        Name = value.Name;
        Manufacturer = value.Manufacturer ?? "";
        PackSize = value.PackSize ?? "";
        HsnCode = value.HsnCode;
        GstRate = value.GstRate;
        Schedule = value.Schedule;
        RackLocation = value.RackLocation ?? "";
        ReorderLevel = value.ReorderLevel;
        IsActive = value.IsActive;
        UnitsPerPack = value.UnitsPerPack;
        AllowLooseSale = value.AllowLooseSale;
        Mrp = 0;

        LoadBatchesAsync(value.Id).Forget("Loading batches");
    }

    private async Task LoadBatchesAsync(Guid productId)
    {
        Batches.Clear();
        foreach (var b in await pharmacy.GetSellableBatchesAsync(productId)) Batches.Add(b);
    }

    /// <summary>Receives a whole supplier bill instead of keying it in line by line.</summary>
    [RelayCommand]
    private async Task ImportBillAsync()
    {
        await Safely.RunAsync(async () =>
        {
            var window = new Views.ImportWindow { Owner = Application.Current.MainWindow };
            window.ShowDialog();

            if (!window.Imported) return;

            await FindAsync();
            Status = "Stock imported. It was added to what was already on the shelf.";
        }, "Importing a supplier bill", m => Status = m);
    }

    [RelayCommand]
    private void NewProduct()
    {
        SelectedProduct = null;
        Name = Manufacturer = PackSize = RackLocation = "";
        HsnCode = "3004";
        GstRate = 12m;
        Schedule = DrugSchedule.None;
        ReorderLevel = 0;
        IsActive = true;
        UnitsPerPack = 1;
        AllowLooseSale = true;
        Status = "Enter the medicine name and save.";
    }

    [RelayCommand]
    private async Task SaveProductAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Warn("Medicine name is required.");
            return;
        }

        var product = SelectedProduct ?? new Product();
        product.Name = Name.Trim();
        product.Manufacturer = Empty(Manufacturer);
        product.PackSize = Empty(PackSize);
        product.HsnCode = string.IsNullOrWhiteSpace(HsnCode) ? "3004" : HsnCode.Trim();
        product.GstRate = GstRate;
        product.Schedule = Schedule;
        product.RackLocation = Empty(RackLocation);
        product.ReorderLevel = ReorderLevel;
        product.IsActive = IsActive;
        product.UnitsPerPack = Math.Max(1, UnitsPerPack);
        product.AllowLooseSale = AllowLooseSale && product.UnitsPerPack > 1;

        await pharmacy.SaveProductAsync(product);
        Status = $"{product.Name} saved.";

        Search = product.Name;
        await FindAsync();
        SelectedProduct = Products.FirstOrDefault(p => p.Id == product.Id);
    }

    [RelayCommand]
    private async Task AddStockAsync()
    {
        if (SelectedProduct is null)
        {
            Warn("Select a medicine first, then add its stock.");
            return;
        }

        if (string.IsNullOrWhiteSpace(BatchNo))
        {
            Warn("Batch number is printed on the pack and is required on every bill.");
            return;
        }

        if (Quantity <= 0 && FreeQuantity <= 0)
        {
            Warn("Enter the quantity received.");
            return;
        }

        if (Mrp <= 0)
        {
            Warn("Enter the MRP printed on the pack — the sale price comes from it.");
            return;
        }

        if (ExpiryDate.Date <= DateTime.Today)
        {
            Warn("Expiry date must be in the future.");
            return;
        }

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
            Quantity = Quantity,
            FreeQuantity = FreeQuantity,
            // Without this the batch was stocked in packs while the counter priced
            // and sold in units, so ten strips became ten tablets and every sale
            // charged a whole strip. The import path always set it; this one did not.
            UnitsPerPack = SelectedProduct.UnitsPerPack,
            PurchaseRate = PurchaseRate,
            Mrp = Mrp
        };

        try
        {
            await pharmacy.ReceiveStockAsync(entry, [item]);
            Status = $"{item.UnitsReceived} unit(s) of {SelectedProduct.Name} added to batch {item.BatchNo} " +
                     $"({Quantity + FreeQuantity} × {SelectedProduct.UnitsPerPack}).";

            BatchNo = "";
            Quantity = FreeQuantity = 0;
            PurchaseRate = 0;

            await LoadBatchesAsync(SelectedProduct.Id);
            await FindAsync();
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    /// <summary>
    /// Corrects what a batch holds. Stock otherwise only moves by receiving or
    /// selling, both of which leave a document — a correction writes its own, so
    /// a shortfall can always be explained.
    /// </summary>
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

    private async Task LoadAdjustmentsAsync()
    {
        Adjustments.Clear();
        foreach (var a in await pharmacy.GetAdjustmentsAsync(100)) Adjustments.Add(a);
    }

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Warn(string message)
    {
        Status = message;
        MessageBox.Show(message, "Medicines", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
