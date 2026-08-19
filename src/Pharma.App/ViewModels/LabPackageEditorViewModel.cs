using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// A package — "Full Body Checkup" — added or edited over the shell, with
/// its report membership replaced wholesale on Save. Same shape as
/// <see cref="LabReportEditorViewModel"/>, one level up.
/// </summary>
public partial class LabPackageEditorViewModel : ObservableObject
{
    private readonly PathologyLabService _lab;
    private readonly LabPackageMaster? _existing;

    public LabPackageEditorViewModel(PathologyLabService lab, LabPackageMaster? existing = null)
    {
        _lab = lab;
        _existing = existing;

        if (existing is null) return;

        Name = existing.Name;
        PackagePrice = existing.PackagePrice;
        Active = existing.Active;
    }

    public string Header => _existing is null ? "New package" : _existing.Name;
    public bool CanDelete => _existing is not null;

    public event Action? RequestClose;
    public string? Outcome { get; private set; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private decimal _packagePrice;
    [ObservableProperty] private bool _active = true;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _nameMissing;

    public ObservableCollection<LabReport> Reports { get; } = [];
    [ObservableProperty] private LabReport? _selectedReportToAdd;

    public ObservableCollection<LabReport> Members { get; } = [];

    public async Task LoadAsync()
    {
        Reports.Clear();
        foreach (var r in await _lab.SearchReportsAsync(null, activeOnly: true)) Reports.Add(r);

        Members.Clear();
        if (_existing is not null)
            foreach (var r in await _lab.GetPackageReportsAsync(_existing.Id)) Members.Add(r);
    }

    partial void OnNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) NameMissing = false;
    }

    [RelayCommand]
    private void AddMember()
    {
        if (SelectedReportToAdd is not { } report) return;
        if (Members.Any(m => m.Id == report.Id)) return;

        Members.Add(report);
        SelectedReportToAdd = null;
    }

    [RelayCommand]
    private void RemoveMember(LabReport? report)
    {
        if (report is not null) Members.Remove(report);
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

        if (Members.Count == 0)
        {
            Warn("Add at least one report to the package.");
            return;
        }

        NameMissing = false;

        var package = _existing ?? new LabPackageMaster();
        package.Name = Name.Trim();
        package.PackagePrice = PackagePrice;
        package.Active = Active;

        try
        {
            await _lab.SavePackageAsync(package, Members.Select(m => m.Id).ToList());
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
            await _lab.DeletePackageAsync(_existing.Id);
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
