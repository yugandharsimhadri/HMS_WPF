using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// The medicine catalogue — what a medicine is, not how much of it there is.
///
/// Stock lives on the Inventory screen. Setting a medicine up happens once;
/// receiving and correcting stock happens every delivery, often by someone else.
/// </summary>
public partial class ProductsViewModel(PharmacyService pharmacy) : ObservableObject, IPage
{
    public string Title => "Medicines";
    public string Subtitle => $"{Products.Count} medicine(s) in the catalogue";

    public ObservableCollection<Product> Products { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private string _status = "";

    // The medicine itself
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _genericName = "";
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
    [ObservableProperty] private DispensingUnit _dispensingUnit = DispensingUnit.Tablet;

    public Array DispensingUnitOptions => Enum.GetValues<DispensingUnit>();
    public Array Schedules => Enum.GetValues<DrugSchedule>();
    public bool HasProduct => SelectedProduct is not null;

    /// <summary>Reads back what a pack means, e.g. "15 tablets per pack".</summary>
    [ObservableProperty] private string _packHint = "";

    partial void OnUnitsPerPackChanged(int value) => UpdatePackHint();
    partial void OnDispensingUnitChanged(DispensingUnit value) => UpdatePackHint();

    private void UpdatePackHint()
    {
        var perPack = Math.Max(1, UnitsPerPack);

        PackHint = perPack > 1
            ? $"One pack holds {perPack} {DispensingUnit.Name(perPack)}. " +
              $"Stock and sales are counted in {DispensingUnit.Name(2)}."
            : $"Sold as a single {DispensingUnit.Name(1)} — a pack is one unit.";
    }

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
        if (value is null) return;

        Name = value.Name;
        GenericName = value.GenericName ?? "";
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
        DispensingUnit = value.DispensingUnit;

        UpdatePackHint();
    }

    [RelayCommand]
    private void NewProduct()
    {
        SelectedProduct = null;
        Name = GenericName = Manufacturer = PackSize = RackLocation = "";
        HsnCode = "3004";
        GstRate = 12m;
        Schedule = DrugSchedule.None;
        ReorderLevel = 0;
        IsActive = true;
        UnitsPerPack = 1;
        AllowLooseSale = true;
        DispensingUnit = DispensingUnit.Tablet;

        UpdatePackHint();
        Status = "Enter the brand name and save. Add its stock on the Inventory screen.";
    }

    [RelayCommand]
    private async Task SaveProductAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Warn("The brand name is required.");
            return;
        }

        await Safely.RunAsync(async () =>
        {
            var product = SelectedProduct ?? new Product();

            product.Name = Name.Trim();
            product.GenericName = Empty(GenericName);
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
            product.DispensingUnit = DispensingUnit;

            await pharmacy.SaveProductAsync(product);
            Status = $"{product.Name} saved.";

            Search = product.Name;
            await FindAsync();
            SelectedProduct = Products.FirstOrDefault(p => p.Id == product.Id);
        }, "Saving the medicine", m => Status = m);
    }

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Warn(string message)
    {
        Status = message;
        MessageBox.Show(message, "Medicines", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
