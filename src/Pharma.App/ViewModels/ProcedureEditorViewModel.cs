using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// One procedure master row, added or edited over the shell — shared by
/// Pediatrics and Dentist, same "fresh view model per open" shape as
/// <see cref="DiagnosticTestEditorViewModel"/>. <see cref="ProcedureDepartment"/>
/// defaults to whichever department pill was selected back on General
/// Master, but is still picked explicitly here for a new procedure — the
/// department a row belongs to should never depend on the desk having
/// remembered which pill happened to be highlighted. Fixed once a
/// procedure already exists, though: a procedure never moves between
/// modules by being edited from either one's own screen.
/// </summary>
public partial class ProcedureEditorViewModel : ObservableObject
{
    private readonly ProcedureBillsService _bills;
    private readonly Procedure? _existing;

    public ProcedureEditorViewModel(
        ProcedureBillsService bills, ProcedureDepartment department, IEnumerable<ProcedureDepartment> availableDepartments,
        IEnumerable<string> categories, Procedure? existing = null)
    {
        _bills = bills;
        _existing = existing;

        foreach (var d in availableDepartments) Departments.Add(d);
        SelectedDepartment = existing?.Department ?? department;

        foreach (var c in categories) Categories.Add(c);

        if (existing is null) return;

        Name = existing.Name;
        Category = existing.Category;
        Price = existing.Price;
        Active = existing.Active;
    }

    public string Header => _existing is null ? "New procedure" : _existing.Name;
    public bool CanDelete => _existing is not null;
    public bool IsNewProcedure => _existing is null;

    public ObservableCollection<ProcedureDepartment> Departments { get; } = [];
    [ObservableProperty] private ProcedureDepartment _selectedDepartment;

    public ObservableCollection<string> Categories { get; } = [];

    public event Action? RequestClose;
    public string? Outcome { get; private set; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private bool _active = true;
    [ObservableProperty] private string _status = "";

    [ObservableProperty] private bool _nameMissing;

    partial void OnNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) NameMissing = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            NameMissing = true;
            Warn("Procedure name is required.");
            return;
        }

        NameMissing = false;

        var procedure = _existing ?? new Procedure { Department = SelectedDepartment };
        procedure.Name = Name.Trim();
        procedure.Category = string.IsNullOrWhiteSpace(Category) ? "Others" : Category.Trim();
        procedure.Price = Price;
        procedure.Active = Active;

        try
        {
            await _bills.SaveProcedureAsync(procedure);
            Outcome = $"{procedure.Name} saved.";
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
            $"Delete {_existing.Name}?", "Procedure Master", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _bills.DeleteProcedureAsync(_existing.Id);
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
        Dialog.Show(message, "Procedure Master", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
