using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// One vaccine schedule row, added or edited over the shell — same "fresh
/// view model per open" shape as <see cref="DiagnosticTestEditorViewModel"/>.
/// </summary>
public partial class VaccineEditorViewModel : ObservableObject
{
    private readonly PediatricsService _pediatrics;
    private readonly VaccineMaster? _existing;

    public VaccineEditorViewModel(PediatricsService pediatrics, IEnumerable<string> categories, VaccineMaster? existing = null)
    {
        _pediatrics = pediatrics;
        _existing = existing;

        foreach (var c in categories) Categories.Add(c);

        if (existing is null) return;

        Name = existing.Name;
        DoseNumber = existing.DoseNumber;
        RecommendedAgeDays = existing.RecommendedAgeDays;
        Category = existing.Category;
        SequenceOrder = existing.SequenceOrder;
        Active = existing.Active;
    }

    public string Header => _existing is null ? "New vaccine" : _existing.Name;
    public bool CanDelete => _existing is not null;

    public ObservableCollection<string> Categories { get; } = [];

    public event Action? RequestClose;
    public string? Outcome { get; private set; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private int _doseNumber = 1;
    [ObservableProperty] private int _recommendedAgeDays;
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private int _sequenceOrder;
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
            Warn("Vaccine name is required.");
            return;
        }

        NameMissing = false;

        var vaccine = _existing ?? new VaccineMaster();
        vaccine.Name = Name.Trim();
        vaccine.DoseNumber = DoseNumber;
        vaccine.RecommendedAgeDays = RecommendedAgeDays;
        vaccine.Category = string.IsNullOrWhiteSpace(Category) ? "Others" : Category.Trim();
        vaccine.SequenceOrder = SequenceOrder;
        vaccine.Active = Active;

        try
        {
            await _pediatrics.SaveVaccineAsync(vaccine);
            Outcome = $"{vaccine.Name} saved.";
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
            $"Delete {_existing.Name}?", "Vaccine Master", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _pediatrics.DeleteVaccineAsync(_existing.Id);
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
        Dialog.Show(message, "Vaccine Master", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
