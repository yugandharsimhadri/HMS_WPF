using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;
using System.Windows;

namespace Pharma.App.ViewModels;

/// <summary>
/// One medicine, being added or edited, over the shell.
///
/// A new instance per open, holding the record it was given — the same shape as
/// the consultation. That is what makes the old questions go away: there is no
/// "did the form clear after saving", because closing this is the clearing, and
/// no "is the next medicine overwriting the last one", because the next one is
/// a different object.
///
/// Fifteen fields do not fit a 322px column on a 1366x768 screen without
/// scrolling to reach Save. In three columns over the shell they fit with room
/// to spare.
/// </summary>
public partial class MedicineEditorViewModel : ObservableObject
{
    private readonly PharmacyService _pharmacy;

    /// <summary>The record being edited, or null when this is a new medicine.</summary>
    private readonly Product? _existing;

    public MedicineEditorViewModel(PharmacyService pharmacy, Product? existing = null)
    {
        _pharmacy = pharmacy;
        _existing = existing;

        if (existing is null)
        {
            // A fresh medicine takes its units-per-pack from the pack size as it
            // is typed; an existing one never does — see below.
            _unitsPerPackSetByHand = false;
            UpdatePackHint();
            return;
        }

        Name = existing.Name;
        GenericName = existing.GenericName ?? "";
        Manufacturer = existing.Manufacturer ?? "";
        Composition = existing.Composition ?? "";
        Storage = existing.Storage ?? "";
        PackSize = existing.PackSize ?? "";
        HsnCode = existing.HsnCode;
        GstRate = existing.GstRate;
        Schedule = existing.Schedule;
        RackLocation = existing.RackLocation ?? "";
        ReorderLevel = existing.ReorderLevel;
        IsActive = existing.IsActive;
        AllowLooseSale = existing.AllowLooseSale;
        DispensingUnit = existing.DispensingUnit;

        _fillingIn = true;
        UnitsPerPack = existing.UnitsPerPack;
        _fillingIn = false;

        // Stock already on the shelf is counted against this medicine's
        // units-per-pack, so never quietly change it from the pack size here.
        // Say it looks wrong and let them decide.
        _unitsPerPackSetByHand = true;

        UpdatePackHint();
    }

    public string Header => _existing is null ? "New medicine" : $"Medicine — {_existing.Name}";

    /// <summary>Raised when this should be taken off the shell.</summary>
    public event Action? RequestClose;

    /// <summary>
    /// What the catalogue behind should say once this closes. Null when nothing
    /// was saved, so cancelling leaves the page's own message alone.
    /// </summary>
    public string? Outcome { get; private set; }

    /// <summary>
    /// Set when the save was refused as a duplicate and the operator asked to
    /// open the medicine that already exists. The catalogue reopens on it —
    /// this editor cannot swap the record underneath itself without becoming
    /// the shared, long-lived thing it was written to stop being.
    /// </summary>
    public Guid? OpenInstead { get; private set; }

    // ── The medicine ───────────────────────────────────────────────────────

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _nameMissing;

    partial void OnNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) NameMissing = false;
    }

    [ObservableProperty] private string _genericName = "";
    [ObservableProperty] private string _manufacturer = "";
    [ObservableProperty] private string _composition = "";
    [ObservableProperty] private string _storage = "";
    [ObservableProperty] private string _packSize = "";
    [ObservableProperty] private string _hsnCode = "3004";

    /// <summary>
    /// Zero, not twelve: most of what this clinic stocks is nil-rated, and a
    /// rate filled in for the operator is one they stop reading.
    /// </summary>
    [ObservableProperty] private decimal _gstRate;

    [ObservableProperty] private DrugSchedule _schedule = DrugSchedule.None;
    [ObservableProperty] private string _rackLocation = "";
    [ObservableProperty] private int _reorderLevel;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private int _unitsPerPack = 1;
    [ObservableProperty] private bool _allowLooseSale = true;
    [ObservableProperty] private DispensingUnit _dispensingUnit = DispensingUnit.Tablet;

    public Array DispensingUnitOptions => Enum.GetValues<DispensingUnit>();
    public Array Schedules => Enum.GetValues<DrugSchedule>();

    [ObservableProperty] private string _packHint = "";
    [ObservableProperty] private string _status = "";

    /// <summary>True once the user has typed in Units in one pack themselves.</summary>
    private bool _unitsPerPackSetByHand;

    /// <summary>Set while the code fills a field, so it does not look like typing.</summary>
    private bool _fillingIn;

    partial void OnUnitsPerPackChanged(int value)
    {
        if (!_fillingIn) _unitsPerPackSetByHand = true;
        UpdatePackHint();
    }

    partial void OnDispensingUnitChanged(DispensingUnit value) => UpdatePackHint();

    /// <summary>
    /// "15 TAB" already says fifteen. Leaving Units in one pack at 1 alongside
    /// it makes a strip and a tablet the same thing, so the counter sells whole
    /// strips to anyone asking for tablets — at fifteen times the price, with
    /// nothing anywhere reporting an error.
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

    // ── Saving ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            NameMissing = true;
            Status = "The brand name is required.";
            Dialog.Show(Status, "Medicines", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        NameMissing = false;

        // Caught inside the guarded block and dealt with after it, because a
        // duplicate is not a failure — it is a wrong turn worth offering a way
        // out of.
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

        // Nobody adds a duplicate on purpose — they could not find the first
        // one. So offer to open it rather than just refusing.
        Status = duplicate.Message;

        var answer = Dialog.Show(
            $"{duplicate.Message}\n\nOpen the one that is already there?",
            "Medicines", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes) return;

        OpenInstead = duplicate.Existing.Id;
        RequestClose?.Invoke();
    }

    private async Task SaveTheMedicineAsync()
    {
        var product = _existing ?? new Product();

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

        // Keep what was actually chosen. Forcing it false when a pack holds one
        // unit looks harmless — every sale is a whole pack anyway — but it
        // sticks, so correcting a wrong pack size later left the medicine
        // refusing to be sold loose with no clue why.
        product.AllowLooseSale = AllowLooseSale;
        product.DispensingUnit = DispensingUnit;

        await _pharmacy.SaveProductAsync(product);

        // A re-count has far more to say than "saved", so it speaks instead.
        var repack = await OfferToRepackAsync(product);

        Outcome = repack ?? $"{product.Name} saved.";
        RequestClose?.Invoke();
    }

    /// <summary>
    /// A batch keeps the units-per-pack it was received under, so changing the
    /// medicine on its own leaves stock already on the shelf being sold by the
    /// pack. Offer to re-count it — the packs do not move, only what the
    /// software believes one of them holds.
    /// </summary>
    private async Task<string?> OfferToRepackAsync(Product product)
    {
        var preview = await _pharmacy.PreviewRepackAsync(product.Id, product.UnitsPerPack);
        if (!preview.AnythingToDo) return null;

        var packs = preview.QuantityAfter / Math.Max(1, preview.UnitsPerPack);
        var unit = product.DispensingUnit.Name(preview.QuantityAfter);

        var answer = Dialog.Show(
            $"{preview.Batches} batch(es) of {product.Name} on the shelf were received " +
            $"under a different pack size, so the counter still sells them by the pack.\n\n" +
            $"Re-count them as {preview.UnitsPerPack} per pack?\n\n" +
            $"    {preview.QuantityBefore} → {preview.QuantityAfter} {unit}\n" +
            $"    ({packs} pack(s) — nothing on the shelf changes)\n\n" +
            $"Every batch is recorded in the correction trail.",
            "Medicines", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return null;

        var repacked = await _pharmacy.RepackAsync(product.Id, product.UnitsPerPack, Environment.UserName);

        return $"{product.Name} saved. {repacked} batch(es) re-counted at " +
               $"{preview.UnitsPerPack} per pack — now {preview.QuantityAfter} {unit} on hand.";
    }

    /// <summary>Closes without saving. Nothing typed here has been written.</summary>
    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
