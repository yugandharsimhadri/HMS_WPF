using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// One shared configuration home for masters that used to sit behind a
/// "Master" destination on each clinical module — Vaccine Master and the
/// Procedure Master both Pediatrics and Dentist bill against, plus the
/// three masters that are Dentist-specific (replacement, anesthesia type,
/// package). Neither module needs its own Master screen any more; each
/// tab here shows itself only when the specialty it belongs to is actually
/// switched on under Settings → Features, read fresh every time this page
/// loads. Pathology Lab keeps its own Master screen deliberately — its
/// analyte/report/package shape doesn't share anything with Procedure, so
/// folding it in here would only be a screen with two unrelated halves.
/// </summary>
public partial class GeneralMasterViewModel(
    PediatricsService pediatrics, DentistService dentist, ProcedureBillsService bills, SettingsService settings)
    : ObservableObject, IPage
{
    public string Title => "General Master";
    public string Subtitle => "Vaccination, procedures, and dentist-specific configuration";

    public async Task LoadAsync()
    {
        var general = await settings.GetGeneralAsync();
        PediatricsEnabled = general.PediatricsEnabled;
        DentistEnabled = general.DentistEnabled;

        // Land on whichever department this clinic actually uses, rather
        // than always defaulting to Pediatrics — a dentist-only clinic
        // opening this for the first time should not see an empty grid.
        SelectedDepartment = PediatricsEnabled ? ProcedureDepartment.Pediatrics
            : DentistEnabled ? ProcedureDepartment.Dentist
            : ProcedureDepartment.General;

        if (PediatricsEnabled) await LoadVaccineMasterAsync();
        await LoadProcedureMasterAsync();

        if (DentistEnabled)
        {
            await LoadReplacementMasterAsync();
            await LoadAnesthesiaMasterAsync();
            await LoadPackageMasterAsync();
        }
    }

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _pediatricsEnabled;
    [ObservableProperty] private bool _dentistEnabled;

    // ── Vaccine master (Pediatrics only) ────────────────────────────────────

    public ObservableCollection<VaccineMaster> VaccineMasterRows { get; } = [];
    [ObservableProperty] private string _vaccineMasterSearch = "";

    [NotifyPropertyChangedFor(nameof(HasMasterVaccine))]
    [ObservableProperty] private VaccineMaster? _selectedMasterVaccine;

    public bool HasMasterVaccine => SelectedMasterVaccine is not null;

    private static readonly string[] ExampleVaccineCategories =
        ["Universal Immunization Programme", "Optional", "Travel", "Catch-up"];

    private async Task LoadVaccineMasterAsync()
    {
        VaccineMasterRows.Clear();
        foreach (var v in await pediatrics.SearchVaccinesAsync(VaccineMasterSearch)) VaccineMasterRows.Add(v);
    }

    [RelayCommand]
    private async Task FindVaccineMasterAsync() => await LoadVaccineMasterAsync();

    partial void OnVaccineMasterSearchChanged(string value) => FindVaccineMasterAsync().Forget("Searching vaccine master");

    [RelayCommand]
    private Task NewVaccineAsync() => EditVaccineAsync(null);

    [RelayCommand]
    private Task EditVaccineAsync() => EditVaccineAsync(SelectedMasterVaccine);

    private async Task EditVaccineAsync(VaccineMaster? existing)
    {
        var categories = ExampleVaccineCategories.Union(await pediatrics.GetVaccineCategoriesAsync()).OrderBy(c => c);
        var editor = new VaccineEditorViewModel(pediatrics, categories, existing);
        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        if (editor.Outcome is { } outcome) Status = outcome;

        await LoadVaccineMasterAsync();
        SelectedMasterVaccine = existing is null ? null : VaccineMasterRows.FirstOrDefault(v => v.Id == existing.Id);
    }

    // ── Procedure master — shared by every department, filtered by a
    //    Pediatrics / General / Dentist pill rather than three separate
    //    screens each hard-coded to one department. ─────────────────────────

    public ObservableCollection<Procedure> ProcedureMasterRows { get; } = [];
    [ObservableProperty] private string _procedureMasterSearch = "";

    [NotifyPropertyChangedFor(nameof(IsPediatricsDepartment))]
    [NotifyPropertyChangedFor(nameof(IsGeneralDepartment))]
    [NotifyPropertyChangedFor(nameof(IsDentistDepartment))]
    [ObservableProperty] private ProcedureDepartment _selectedDepartment = ProcedureDepartment.General;

    public bool IsPediatricsDepartment => SelectedDepartment == ProcedureDepartment.Pediatrics;
    public bool IsGeneralDepartment => SelectedDepartment == ProcedureDepartment.General;
    public bool IsDentistDepartment => SelectedDepartment == ProcedureDepartment.Dentist;

    [NotifyPropertyChangedFor(nameof(HasMasterProcedure))]
    [ObservableProperty] private Procedure? _selectedMasterProcedure;

    public bool HasMasterProcedure => SelectedMasterProcedure is not null;

    private static readonly string[] PediatricsExampleCategories = ["Minor procedure", "Screening", "Others"];
    private static readonly string[] DentistExampleCategories = ["Restorative", "Endodontic", "Periodontal", "Cosmetic", "Minor surgery"];
    private static readonly string[] GeneralExampleCategories = ["General", "Minor procedure", "Others"];

    private string[] ExampleCategoriesFor(ProcedureDepartment department) => department switch
    {
        ProcedureDepartment.Pediatrics => PediatricsExampleCategories,
        ProcedureDepartment.Dentist => DentistExampleCategories,
        _ => GeneralExampleCategories
    };

    private async Task LoadProcedureMasterAsync()
    {
        ProcedureMasterRows.Clear();
        foreach (var p in await bills.SearchProceduresAsync(SelectedDepartment, ProcedureMasterSearch))
            ProcedureMasterRows.Add(p);
    }

    [RelayCommand]
    private async Task FindProcedureMasterAsync() => await LoadProcedureMasterAsync();

    partial void OnProcedureMasterSearchChanged(string value) => FindProcedureMasterAsync().Forget("Searching procedure master");
    partial void OnSelectedDepartmentChanged(ProcedureDepartment value) => FindProcedureMasterAsync().Forget("Switching department");

    [RelayCommand]
    private void SelectPediatricsDepartment() => SelectedDepartment = ProcedureDepartment.Pediatrics;

    [RelayCommand]
    private void SelectGeneralDepartment() => SelectedDepartment = ProcedureDepartment.General;

    [RelayCommand]
    private void SelectDentistDepartment() => SelectedDepartment = ProcedureDepartment.Dentist;

    [RelayCommand]
    private Task NewProcedureAsync() => EditProcedureAsync(null);

    [RelayCommand]
    private Task EditProcedureAsync() => EditProcedureAsync(SelectedMasterProcedure);

    private async Task EditProcedureAsync(Procedure? existing)
    {
        var department = existing?.Department ?? SelectedDepartment;
        var categories = ExampleCategoriesFor(department).Union(await bills.GetCategoriesAsync(department)).OrderBy(c => c);

        var availableDepartments = new List<ProcedureDepartment>();
        if (PediatricsEnabled) availableDepartments.Add(ProcedureDepartment.Pediatrics);
        availableDepartments.Add(ProcedureDepartment.General);
        if (DentistEnabled) availableDepartments.Add(ProcedureDepartment.Dentist);

        var editor = new ProcedureEditorViewModel(bills, department, availableDepartments, categories, existing);
        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        if (editor.Outcome is { } outcome) Status = outcome;

        // A new procedure may have been saved under a different department
        // than whichever pill was selected — land on it, so it doesn't look
        // like the save silently did nothing.
        if (editor.Outcome is not null) SelectedDepartment = editor.SelectedDepartment;

        await LoadProcedureMasterAsync();
        SelectedMasterProcedure = existing is null ? null : ProcedureMasterRows.FirstOrDefault(p => p.Id == existing.Id);
    }

    // ── Replacement master (Dentist only) ───────────────────────────────────

    public ObservableCollection<DentalReplacementMaster> ReplacementMasterRows { get; } = [];
    [ObservableProperty] private string _replacementMasterSearch = "";

    [NotifyPropertyChangedFor(nameof(HasMasterReplacement))]
    [ObservableProperty] private DentalReplacementMaster? _selectedMasterReplacement;

    public bool HasMasterReplacement => SelectedMasterReplacement is not null;

    private static readonly string[] ExampleReplacementCategories = ["Crown", "Bridge", "Denture", "Implant"];

    private async Task LoadReplacementMasterAsync()
    {
        ReplacementMasterRows.Clear();
        foreach (var r in await dentist.SearchReplacementsAsync(ReplacementMasterSearch)) ReplacementMasterRows.Add(r);
    }

    [RelayCommand]
    private async Task FindReplacementMasterAsync() => await LoadReplacementMasterAsync();

    partial void OnReplacementMasterSearchChanged(string value) => FindReplacementMasterAsync().Forget("Searching replacement master");

    [RelayCommand]
    private Task NewReplacementAsync() => EditReplacementAsync(null);

    [RelayCommand]
    private Task EditReplacementAsync() => EditReplacementAsync(SelectedMasterReplacement);

    private async Task EditReplacementAsync(DentalReplacementMaster? existing)
    {
        var categories = ExampleReplacementCategories.AsEnumerable();
        var editor = new DentalReplacementEditorViewModel(dentist, categories, existing);
        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        if (editor.Outcome is { } outcome) Status = outcome;

        await LoadReplacementMasterAsync();
        SelectedMasterReplacement = existing is null ? null : ReplacementMasterRows.FirstOrDefault(r => r.Id == existing.Id);
    }

    // ── Anesthesia type master (Dentist only) ───────────────────────────────

    public ObservableCollection<AnesthesiaTypeMaster> AnesthesiaMasterRows { get; } = [];

    [NotifyPropertyChangedFor(nameof(HasMasterAnesthesiaType))]
    [ObservableProperty] private AnesthesiaTypeMaster? _selectedMasterAnesthesiaType;

    public bool HasMasterAnesthesiaType => SelectedMasterAnesthesiaType is not null;

    private async Task LoadAnesthesiaMasterAsync()
    {
        AnesthesiaMasterRows.Clear();
        foreach (var a in await dentist.SearchAnesthesiaTypesAsync()) AnesthesiaMasterRows.Add(a);
    }

    [RelayCommand]
    private Task NewAnesthesiaTypeAsync() => EditAnesthesiaTypeAsync(null);

    [RelayCommand]
    private Task EditAnesthesiaTypeAsync() => EditAnesthesiaTypeAsync(SelectedMasterAnesthesiaType);

    private async Task EditAnesthesiaTypeAsync(AnesthesiaTypeMaster? existing)
    {
        var editor = new AnesthesiaTypeEditorViewModel(dentist, existing);
        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        if (editor.Outcome is { } outcome) Status = outcome;

        await LoadAnesthesiaMasterAsync();
        SelectedMasterAnesthesiaType = existing is null ? null : AnesthesiaMasterRows.FirstOrDefault(a => a.Id == existing.Id);
    }

    // ── Package master (Dentist only) ───────────────────────────────────────

    public ObservableCollection<DentalPackageMaster> PackageMasterRows { get; } = [];

    [NotifyPropertyChangedFor(nameof(HasMasterPackage))]
    [ObservableProperty] private DentalPackageMaster? _selectedMasterPackage;

    public bool HasMasterPackage => SelectedMasterPackage is not null;

    private async Task LoadPackageMasterAsync()
    {
        PackageMasterRows.Clear();
        foreach (var p in await dentist.SearchPackagesAsync(null)) PackageMasterRows.Add(p);
    }

    [RelayCommand]
    private Task NewPackageAsync() => EditPackageAsync(null);

    [RelayCommand]
    private Task EditPackageAsync() => EditPackageAsync(SelectedMasterPackage);

    private async Task EditPackageAsync(DentalPackageMaster? existing)
    {
        var editor = new DentalPackageEditorViewModel(dentist, bills, existing);
        await editor.LoadAsync();

        var shell = App.Services.GetRequiredService<MainViewModel>();
        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        if (editor.Outcome is { } outcome) Status = outcome;

        await LoadPackageMasterAsync();
        SelectedMasterPackage = existing is null ? null : PackageMasterRows.FirstOrDefault(p => p.Id == existing.Id);
    }
}
