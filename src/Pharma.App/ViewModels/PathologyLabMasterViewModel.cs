using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// Pathology Lab configuration, not per-visit work: the analyte, report
/// (panel) and package masters — split out of
/// <see cref="PathologyLabViewModel"/> into its own nav destination
/// ("Master") so day-to-day order work never shares a screen with clinic
/// setup.
/// </summary>
public partial class PathologyLabMasterViewModel(PathologyLabService lab) : ObservableObject, IPage
{
    public string Title => "Pathology Lab — Master";
    public string Subtitle => "Analyte, report and package master";

    public async Task LoadAsync()
    {
        await LoadAnalyteMasterAsync();
        await LoadReportMasterAsync();
        await LoadPackageMasterAsync();
    }

    [ObservableProperty] private string _status = "";

    // ── Analyte master ──────────────────────────────────────────────────────

    public ObservableCollection<LabAnalyte> AnalyteMasterRows { get; } = [];
    [ObservableProperty] private string _analyteMasterSearch = "";

    [NotifyPropertyChangedFor(nameof(HasMasterAnalyte))]
    [ObservableProperty] private LabAnalyte? _selectedMasterAnalyte;

    public bool HasMasterAnalyte => SelectedMasterAnalyte is not null;

    private async Task LoadAnalyteMasterAsync()
    {
        AnalyteMasterRows.Clear();
        foreach (var a in await lab.SearchAnalytesAsync(AnalyteMasterSearch)) AnalyteMasterRows.Add(a);
    }

    [RelayCommand]
    private async Task FindAnalyteMasterAsync() => await LoadAnalyteMasterAsync();

    partial void OnAnalyteMasterSearchChanged(string value) => FindAnalyteMasterAsync().Forget("Searching analyte master");

    [RelayCommand]
    private Task NewAnalyteAsync() => EditAnalyteAsync(null);

    [RelayCommand]
    private Task EditAnalyteAsync() => EditAnalyteAsync(SelectedMasterAnalyte);

    private async Task EditAnalyteAsync(LabAnalyte? existing)
    {
        var categories = await lab.GetAnalyteCategoriesAsync();
        var editor = new LabAnalyteEditorViewModel(lab, categories, existing);
        await editor.LoadAsync();

        var shell = App.Services.GetRequiredService<MainViewModel>();
        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        if (editor.Outcome is { } outcome) Status = outcome;

        await LoadAnalyteMasterAsync();
        SelectedMasterAnalyte = existing is null ? null : AnalyteMasterRows.FirstOrDefault(a => a.Id == existing.Id);
    }

    // ── Report master ───────────────────────────────────────────────────────

    public ObservableCollection<LabReport> ReportMasterRows { get; } = [];

    [NotifyPropertyChangedFor(nameof(HasMasterReport))]
    [ObservableProperty] private LabReport? _selectedMasterReport;

    public bool HasMasterReport => SelectedMasterReport is not null;

    private static readonly string[] ExampleReportCategories = ["Hematology", "Biochemistry", "Serology", "Microbiology", "Hormone"];

    private async Task LoadReportMasterAsync()
    {
        ReportMasterRows.Clear();
        foreach (var r in await lab.SearchReportsAsync(null)) ReportMasterRows.Add(r);
    }

    [RelayCommand]
    private Task NewReportAsync() => EditReportAsync(null);

    [RelayCommand]
    private Task EditReportAsync() => EditReportAsync(SelectedMasterReport);

    private async Task EditReportAsync(LabReport? existing)
    {
        var categories = ExampleReportCategories.Union(ReportMasterRows.Select(r => r.Category)).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c);
        var editor = new LabReportEditorViewModel(lab, categories, existing);
        await editor.LoadAsync();

        var shell = App.Services.GetRequiredService<MainViewModel>();
        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        if (editor.Outcome is { } outcome) Status = outcome;

        await LoadReportMasterAsync();
        SelectedMasterReport = existing is null ? null : ReportMasterRows.FirstOrDefault(r => r.Id == existing.Id);
    }

    // ── Package master ──────────────────────────────────────────────────────

    public ObservableCollection<LabPackageMaster> PackageMasterRows { get; } = [];

    [NotifyPropertyChangedFor(nameof(HasMasterPackage))]
    [ObservableProperty] private LabPackageMaster? _selectedMasterPackage;

    public bool HasMasterPackage => SelectedMasterPackage is not null;

    private async Task LoadPackageMasterAsync()
    {
        PackageMasterRows.Clear();
        foreach (var p in await lab.SearchPackagesAsync(null)) PackageMasterRows.Add(p);
    }

    [RelayCommand]
    private Task NewPackageAsync() => EditPackageAsync(null);

    [RelayCommand]
    private Task EditPackageAsync() => EditPackageAsync(SelectedMasterPackage);

    private async Task EditPackageAsync(LabPackageMaster? existing)
    {
        var editor = new LabPackageEditorViewModel(lab, existing);
        await editor.LoadAsync();

        var shell = App.Services.GetRequiredService<MainViewModel>();
        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        if (editor.Outcome is { } outcome) Status = outcome;

        await LoadPackageMasterAsync();
        SelectedMasterPackage = existing is null ? null : PackageMasterRows.FirstOrDefault(p => p.Id == existing.Id);
    }
}
