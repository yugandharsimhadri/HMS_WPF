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

    public Array Schedules => Enum.GetValues<DrugSchedule>();
    public bool HasProduct => SelectedProduct is not null;

    public async Task LoadAsync() => await FindAsync();

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
        Mrp = 0;

        LoadBatchesAsync(value.Id).Forget("Loading batches");
    }

    private async Task LoadBatchesAsync(Guid productId)
    {
        Batches.Clear();
        foreach (var b in await pharmacy.GetSellableBatchesAsync(productId)) Batches.Add(b);
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
            PurchaseRate = PurchaseRate,
            Mrp = Mrp
        };

        try
        {
            await pharmacy.ReceiveStockAsync(entry, [item]);
            Status = $"{Quantity + FreeQuantity} unit(s) of {SelectedProduct.Name} added to batch {item.BatchNo}.";

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

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Warn(string message)
    {
        Status = message;
        MessageBox.Show(message, "Medicines", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
