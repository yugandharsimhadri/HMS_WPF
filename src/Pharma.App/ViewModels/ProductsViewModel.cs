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
    [ObservableProperty] private string _composition = "";
    [ObservableProperty] private string _storage = "";
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

    /// <summary>True once the user has typed in Units in one pack themselves.</summary>
    private bool _unitsPerPackSetByHand;

    partial void OnUnitsPerPackChanged(int value)
    {
        if (!_fillingIn) _unitsPerPackSetByHand = true;
        UpdatePackHint();
    }

    partial void OnDispensingUnitChanged(DispensingUnit value) => UpdatePackHint();

    /// <summary>
    /// "15 TAB" already says fifteen. Leaving Units in one pack at 1 alongside it
    /// makes a strip and a tablet the same thing, so the counter sells whole
    /// strips to anyone asking for tablets — at fifteen times the price, with
    /// nothing anywhere reporting an error. Take the number from the pack size
    /// unless the user has stated one themselves.
    /// </summary>
    partial void OnPackSizeChanged(string value)
    {
        if (!_unitsPerPackSetByHand && PackMath.UnitsFromPacking(value) is { } stated)
        {
            _fillingIn = true;
            UnitsPerPack = stated;
            _fillingIn = false;
        }

        UpdatePackHint();
    }

    private void UpdatePackHint()
    {
        var perPack = Math.Max(1, UnitsPerPack);
        var stated = PackMath.UnitsFromPacking(PackSize);

        // The one combination that silently overcharges every customer.
        if (stated is { } n && n != perPack)
        {
            PackHint = $"⚠ Pack size says {n} but one pack is set to {perPack}. " +
                       $"At {perPack} the counter would sell whole packs to anyone asking " +
                       $"for {DispensingUnit.Name(2)}. Set it to {n} unless this is right.";
            return;
        }

        PackHint = perPack > 1
            ? $"One pack holds {perPack} {DispensingUnit.Name(perPack)}. " +
              $"Stock and sales are counted in {DispensingUnit.Name(2)}."
            : $"Sold as a single {DispensingUnit.Name(1)} — a pack is one unit.";
    }

    /// <summary>Set while the code fills a field, so it does not look like typing.</summary>
    private bool _fillingIn;

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
        Composition = value.Composition ?? "";
        Storage = value.Storage ?? "";
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

        // An existing medicine has stock counted against its units-per-pack, so
        // never quietly change it here — say it looks wrong and let them decide.
        _unitsPerPackSetByHand = true;

        UpdatePackHint();
    }

    [RelayCommand]
    private async Task NewProductAsync()
    {
        SelectedProduct = null;
        Name = GenericName = Manufacturer = PackSize = RackLocation = "";
        Composition = Storage = "";
        HsnCode = "3004";
        GstRate = 12m;
        Schedule = DrugSchedule.None;
        ReorderLevel = 0;
        IsActive = true;
        _fillingIn = true;
        UnitsPerPack = 1;
        _fillingIn = false;

        // A fresh medicine takes its units-per-pack from the pack size as it is typed.
        _unitsPerPackSetByHand = false;

        AllowLooseSale = true;
        DispensingUnit = DispensingUnit.Tablet;

        UpdatePackHint();

        // Clearing the form while leaving the search box full left the list still
        // filtered, so the screen did not look cleared at all. Empty the box and
        // show the whole catalogue again.
        Search = "";
        await FindAsync();

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

        // Caught inside the guarded block and dealt with after it, because a
        // duplicate is not a failure — it is a wrong turn worth offering a way out of.
        DuplicateMedicineException? duplicate = null;

        await Safely.RunAsync(async () =>
        {
            try
            {
                await SaveTheMedicineAsync();
            }
            catch (DuplicateMedicineException ex)
            {
                duplicate = ex;
            }
        }, "Saving the medicine", m => Status = m);

        if (duplicate is null) return;

        // Nobody adds a duplicate on purpose — they could not find the first one.
        // So offer to open it rather than just refusing.
        Status = duplicate.Message;

        var answer = MessageBox.Show(
            $"{duplicate.Message}\n\nOpen the one that is already there?",
            "Medicines", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes) return;

        Search = duplicate.Existing.Name;
        await FindAsync();
        SelectedProduct = Products.FirstOrDefault(p => p.Id == duplicate.Existing.Id);
    }

    private async Task SaveTheMedicineAsync()
    {
        {
            var product = SelectedProduct ?? new Product();

            product.Name = Name.Trim();
            product.GenericName = Empty(GenericName);
            product.Manufacturer = Empty(Manufacturer);
            product.Composition = Empty(Composition);
            product.Storage = Empty(Storage);
            product.PackSize = Empty(PackSize);
            product.HsnCode = string.IsNullOrWhiteSpace(HsnCode) ? "3004" : HsnCode.Trim();
            product.GstRate = GstRate;
            product.Schedule = Schedule;
            product.RackLocation = Empty(RackLocation);
            product.ReorderLevel = ReorderLevel;
            product.IsActive = IsActive;
            product.UnitsPerPack = Math.Max(1, UnitsPerPack);
            // Keep what was actually chosen. Forcing it false when a pack holds
            // one unit looks harmless — every sale is a whole pack anyway — but
            // it sticks, so correcting a wrong pack size later left the medicine
            // refusing to be sold loose with no clue why.
            product.AllowLooseSale = AllowLooseSale;
            product.DispensingUnit = DispensingUnit;

            await pharmacy.SaveProductAsync(product);
            Status = $"{product.Name} saved.";

            await OfferToRepackAsync(product);

            Search = product.Name;
            await FindAsync();
            SelectedProduct = Products.FirstOrDefault(p => p.Id == product.Id);
        }
    }

    /// <summary>
    /// A batch keeps the units-per-pack it was received under, so changing the
    /// medicine on its own leaves stock already on the shelf being sold by the
    /// pack. Offer to re-count it — the packs do not move, only what the
    /// software believes one of them holds.
    /// </summary>
    private async Task OfferToRepackAsync(Product product)
    {
        var preview = await pharmacy.PreviewRepackAsync(product.Id, product.UnitsPerPack);
        if (!preview.AnythingToDo) return;

        var packs = preview.QuantityAfter / Math.Max(1, preview.UnitsPerPack);
        var unit = product.DispensingUnit.Name(preview.QuantityAfter);

        var answer = MessageBox.Show(
            $"{preview.Batches} batch(es) of {product.Name} on the shelf were received " +
            $"under a different pack size, so the counter still sells them by the pack.\n\n" +
            $"Re-count them as {preview.UnitsPerPack} per pack?\n\n" +
            $"    {preview.QuantityBefore} → {preview.QuantityAfter} {unit}\n" +
            $"    ({packs} pack(s) — nothing on the shelf changes)\n\n" +
            $"Every batch is recorded in the correction trail.",
            "Medicines", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        var repacked = await pharmacy.RepackAsync(product.Id, product.UnitsPerPack, Environment.UserName);

        Status = $"{product.Name} saved. {repacked} batch(es) re-counted at " +
                 $"{preview.UnitsPerPack} per pack — now {preview.QuantityAfter} {unit} on hand.";
    }

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Warn(string message)
    {
        Status = message;
        MessageBox.Show(message, "Medicines", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
