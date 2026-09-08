using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>One replacement master row, added or edited over the shell —
/// same shape as <see cref="VaccineEditorViewModel"/>.</summary>
public partial class DentalReplacementEditorViewModel : ObservableObject
{
    private readonly DentistService _dentist;
    private readonly DentalReplacementMaster? _existing;

    public DentalReplacementEditorViewModel(DentistService dentist, IEnumerable<string> categories, DentalReplacementMaster? existing = null)
    {
        _dentist = dentist;
        _existing = existing;

        foreach (var c in categories) Categories.Add(c);

        if (existing is null) return;

        Name = existing.Name;
        Category = existing.Category;
        UnitCost = existing.UnitCost;
        Active = existing.Active;
    }

    public string Header => _existing is null ? "New replacement" : _existing.Name;
    public bool CanDelete => _existing is not null;

    public ObservableCollection<string> Categories { get; } = [];

    public event Action? RequestClose;
    public string? Outcome { get; private set; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private decimal _unitCost;
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
            Warn("Replacement name is required.");
            return;
        }

        NameMissing = false;

        var replacement = _existing ?? new DentalReplacementMaster();
        replacement.Name = Name.Trim();
        replacement.Category = string.IsNullOrWhiteSpace(Category) ? "Others" : Category.Trim();
        replacement.UnitCost = UnitCost;
        replacement.Active = Active;

        try
        {
            await _dentist.SaveReplacementAsync(replacement);
            Outcome = $"{replacement.Name} saved.";
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
            $"Delete {_existing.Name}?", "Replacement Master", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _dentist.DeleteReplacementAsync(_existing.Id);
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
        Dialog.Show(message, "Replacement Master", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
