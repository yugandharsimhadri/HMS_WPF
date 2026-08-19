using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// A report (panel/profile) — "CBP", "Thyroid Profile" — added or edited
/// over the shell, with its analyte membership. The membership <em>is</em>
/// what the panel is, so unlike <see cref="LabAnalyteEditorViewModel"/>'s
/// reference ranges it is replaced wholesale on Save rather than diffed —
/// same shape as <see cref="DentalPackageEditorViewModel"/>'s procedure list.
/// </summary>
public partial class LabReportEditorViewModel : ObservableObject
{
    private readonly PathologyLabService _lab;
    private readonly LabReport? _existing;

    public LabReportEditorViewModel(PathologyLabService lab, IEnumerable<string> categories, LabReport? existing = null)
    {
        _lab = lab;
        _existing = existing;

        foreach (var c in categories) Categories.Add(c);

        if (existing is null) return;

        Name = existing.Name;
        Category = existing.Category;
        Price = existing.Price;
        SequenceOrder = existing.SequenceOrder;
        Active = existing.Active;
    }

    public string Header => _existing is null ? "New report" : _existing.Name;
    public bool CanDelete => _existing is not null;

    public ObservableCollection<string> Categories { get; } = [];

    public event Action? RequestClose;
    public string? Outcome { get; private set; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private int _sequenceOrder;
    [ObservableProperty] private bool _active = true;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _nameMissing;

    public ObservableCollection<LabAnalyte> Analytes { get; } = [];
    [ObservableProperty] private LabAnalyte? _selectedAnalyteToAdd;

    public ObservableCollection<LabAnalyte> Members { get; } = [];

    public async Task LoadAsync()
    {
        Analytes.Clear();
        foreach (var a in await _lab.SearchAnalytesAsync(null, activeOnly: true)) Analytes.Add(a);

        Members.Clear();
        if (_existing is not null)
            foreach (var a in await _lab.GetReportAnalytesAsync(_existing.Id)) Members.Add(a);
    }

    partial void OnNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) NameMissing = false;
    }

    [RelayCommand]
    private void AddMember()
    {
        if (SelectedAnalyteToAdd is not { } analyte) return;
        if (Members.Any(m => m.Id == analyte.Id)) return;

        Members.Add(analyte);
        SelectedAnalyteToAdd = null;
    }

    [RelayCommand]
    private void RemoveMember(LabAnalyte? analyte)
    {
        if (analyte is not null) Members.Remove(analyte);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            NameMissing = true;
            Warn("Report name is required.");
            return;
        }

        if (Members.Count == 0)
        {
            Warn("Add at least one analyte to the report.");
            return;
        }

        NameMissing = false;

        var report = _existing ?? new LabReport();
        report.Name = Name.Trim();
        report.Category = Category.Trim();
        report.Price = Price;
        report.SequenceOrder = SequenceOrder;
        report.Active = Active;

        try
        {
            await _lab.SaveReportAsync(report, Members.Select(m => m.Id).ToList());
            Outcome = $"{report.Name} saved.";
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
            $"Delete {_existing.Name}?", "Report Master", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _lab.DeleteReportAsync(_existing.Id);
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
        Dialog.Show(message, "Report Master", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
