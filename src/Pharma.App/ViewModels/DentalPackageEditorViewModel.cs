using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>One package line shown in the editor's breakdown list —
/// reference only, never billed from directly.</summary>
public partial class DentalPackageItemRow : ObservableObject
{
    public Guid? ProcedureId { get; init; }
    public string ProcedureName { get; init; } = "";
    [ObservableProperty] private int _quantity = 1;
}

/// <summary>
/// A package, added or edited over the shell, with its reference item
/// breakdown. The breakdown is informational only — see
/// <see cref="DentalPackageMaster"/>'s own class doc for why billing always
/// uses the flat package price instead.
/// </summary>
public partial class DentalPackageEditorViewModel : ObservableObject
{
    private readonly DentistService _dentist;
    private readonly ProcedureBillsService _bills;
    private readonly DentalPackageMaster? _existing;

    public DentalPackageEditorViewModel(
        DentistService dentist, ProcedureBillsService bills, DentalPackageMaster? existing = null)
    {
        _dentist = dentist;
        _bills = bills;
        _existing = existing;

        if (existing is null) return;

        Name = existing.Name;
        Description = existing.Description ?? "";
        PackagePrice = existing.PackagePrice;
        Active = existing.Active;
    }

    public string Header => _existing is null ? "New package" : _existing.Name;
    public bool CanDelete => _existing is not null;

    public event Action? RequestClose;
    public string? Outcome { get; private set; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private decimal _packagePrice;
    [ObservableProperty] private bool _active = true;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _nameMissing;

    public ObservableCollection<Procedure> Procedures { get; } = [];
    [ObservableProperty] private Procedure? _selectedProcedureToAdd;

    public ObservableCollection<DentalPackageItemRow> Items { get; } = [];

    public async Task LoadAsync()
    {
        Procedures.Clear();
        foreach (var p in await _bills.SearchProceduresAsync(ProcedureDepartment.Dentist, null, activeOnly: true))
            Procedures.Add(p);

        Items.Clear();
        if (_existing is not null)
            foreach (var item in await _dentist.GetPackageItemsAsync(_existing.Id))
                Items.Add(new DentalPackageItemRow { ProcedureId = item.ProcedureId, ProcedureName = item.ProcedureName, Quantity = item.Quantity });
    }

    partial void OnNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) NameMissing = false;
    }

    [RelayCommand]
    private void AddItem()
    {
        if (SelectedProcedureToAdd is not { } procedure) return;

        Items.Add(new DentalPackageItemRow { ProcedureId = procedure.Id, ProcedureName = procedure.Name });
        SelectedProcedureToAdd = null;
    }

    [RelayCommand]
    private void RemoveItem(DentalPackageItemRow? row)
    {
        if (row is not null) Items.Remove(row);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            NameMissing = true;
            Warn("Package name is required.");
            return;
        }

        NameMissing = false;

        var package = _existing ?? new DentalPackageMaster();
        package.Name = Name.Trim();
        package.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        package.PackagePrice = PackagePrice;
        package.Active = Active;

        var items = Items.Select(i => new DentalPackageItem
        {
            ProcedureId = i.ProcedureId, ProcedureName = i.ProcedureName, Quantity = i.Quantity
        }).ToList();

        try
        {
            await _dentist.SavePackageAsync(package, items);
            Outcome = $"{package.Name} saved.";
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_existing is null) return;

        var confirm = Dialog.Show(
            $"Delete {_existing.Name}?", "Package Master", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _dentist.DeletePackageAsync(_existing.Id);
            Outcome = $"{_existing.Name} deleted.";
            RequestClose?.Invoke();
        }
        catch (InvalidOperationException ex)
        {
            Warn(ex.Message);
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();

    private void Warn(string message)
    {
        Status = message;
        Dialog.Show(message, "Package Master", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
