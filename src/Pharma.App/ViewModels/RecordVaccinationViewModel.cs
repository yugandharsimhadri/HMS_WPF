using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>What was entered on the form, handed back to
/// <see cref="PediatricsViewModel"/> to both record the dose and add its
/// charge to the current bill — one popup, one action, rather than a
/// separate billing step the desk could forget. The vaccine (the clinical
/// "type", e.g. Hepatitis B dose 2) comes from Vaccine Master; everything
/// about which actual unit was given — brand, batch, price — is the same
/// batch a Pharmacy sale would draw from, never re-entered here.</summary>
public record VaccinationDraft(
    VaccineMaster Vaccine, DateTime GivenOn, string? Site, string? AdministeredBy,
    Guid ProductId, string ProductName, string? Manufacturer, Guid BatchId, string BatchNo, decimal Price);

/// <summary>
/// Records one dose given. The vaccine "type" comes from Vaccine Master; the
/// brand, batch and price are picked from the Pharmacy catalog — the same
/// batch a pharmacy sale would use — rather than typed a second time, so
/// there is only ever one place stock and pricing live. Saving deducts one
/// unit from that batch (see PediatricsService.RecordVaccinationAsync) the
/// same way a counter sale would, since the dose really did leave it.
/// </summary>
public partial class RecordVaccinationViewModel : ObservableObject
{
    private readonly PharmacyService _pharmacy;

    public RecordVaccinationViewModel(IEnumerable<VaccineMaster> vaccines, PharmacyService pharmacy)
    {
        _pharmacy = pharmacy;
        foreach (var v in vaccines) Vaccines.Add(v);
    }

    public string Header => "Record vaccination";

    public event Action? RequestClose;
    public VaccinationDraft? Result { get; private set; }

    public ObservableCollection<VaccineMaster> Vaccines { get; } = [];
    [ObservableProperty] private VaccineMaster? _selectedVaccine;
    [ObservableProperty] private string _givenOnText = DateTime.Today.ToString("yyyy-MM-dd");
    [ObservableProperty] private string _site = "";
    [ObservableProperty] private string _administeredBy = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _vaccineMissing;

    // ── Product (brand/batch) picker — search-as-you-type against the
    //    pharmacy catalog, same idiom as the patient search elsewhere on
    //    this screen. Only products actually in stock are worth showing:
    //    there is nothing to draw the dose from otherwise.

    public ObservableCollection<Product> ProductMatches { get; } = [];
    [ObservableProperty] private string _productSearch = "";

    [NotifyPropertyChangedFor(nameof(HasSelectedProduct))]
    [ObservableProperty] private Product? _selectedProduct;

    [ObservableProperty] private bool _hasProductMatches;
    [ObservableProperty] private bool _productMissing;

    public bool HasSelectedProduct => SelectedProduct is not null;

    /// <summary>What Save will actually charge and deduct — the nearest-
    /// expiry batch's real unit price, the same figure Pharmacy itself
    /// would quote for this product right now.</summary>
    public string SelectedPriceLabel => SelectedProduct?.UnitPriceLabel ?? "";
    public string SelectedBatchLabel => SelectedProduct?.NextBatch is { } b ? $"Batch {b.BatchNo} · exp {b.ExpiryDate:MM/yy}" : "";

    // Set while OnSelectedProductChanged writes ProductSearch back to the
    // picked name — without this, that write re-triggers a search, which
    // replaces ProductMatches with fresh entities and, since the selected
    // item is no longer reference-equal to anything in the new list, WPF
    // silently nulls SelectedItem straight back out from under the picker.
    private bool _syncingProductSearch;

    partial void OnSelectedVaccineChanged(VaccineMaster? value)
    {
        if (value is null) return;
        VaccineMissing = false;
    }

    partial void OnProductSearchChanged(string value)
    {
        if (_syncingProductSearch) return;
        FindProductsAsync().Forget("Searching pharmacy products");
    }

    [RelayCommand]
    private async Task FindProductsAsync()
    {
        ProductMatches.Clear();
        if (!string.IsNullOrWhiteSpace(ProductSearch))
            foreach (var p in await _pharmacy.SearchProductsAsync(ProductSearch, 20))
                if (p.NextBatch is not null) ProductMatches.Add(p);

        HasProductMatches = ProductMatches.Count > 0;
    }

    partial void OnSelectedProductChanged(Product? value)
    {
        OnPropertyChanged(nameof(SelectedPriceLabel));
        OnPropertyChanged(nameof(SelectedBatchLabel));

        if (value is null) return;

        ProductMissing = false;

        _syncingProductSearch = true;
        ProductSearch = value.Name;
        _syncingProductSearch = false;

        // Collapsing the dropdown, not clearing ProductMatches: the picked
        // item still has to live in the ItemsSource the ListBox's
        // SelectedItem points at — remove it from that list and WPF nulls
        // SelectedItem straight back out, undoing the pick.
        HasProductMatches = false;
    }

    [RelayCommand]
    private void ClearProduct()
    {
        SelectedProduct = null;
        ProductSearch = "";
        ProductMatches.Clear();
    }

    [RelayCommand]
    private void Save()
    {
        if (SelectedVaccine is not { } vaccine)
        {
            VaccineMissing = true;
            Status = "Pick a vaccine.";
            return;
        }

        if (SelectedProduct is not { } product || product.NextBatch is not { } batch)
        {
            ProductMissing = true;
            Status = "Pick the brand given, from Pharmacy stock.";
            return;
        }

        var givenOn = DateTime.TryParse(GivenOnText, out var parsed) ? parsed : DateTime.Today;

        Result = new VaccinationDraft(
            vaccine, givenOn,
            string.IsNullOrWhiteSpace(Site) ? null : Site.Trim(),
            string.IsNullOrWhiteSpace(AdministeredBy) ? null : AdministeredBy.Trim(),
            product.Id, product.Name, product.Manufacturer,
            batch.Id, batch.BatchNo, batch.UnitPrice);

        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();
}
