using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>Every screen implements this so the shell can load it uniformly.</summary>
public interface IPage
{
    string Title { get; }
    string Subtitle { get; }
    Task LoadAsync();
}

public partial class MainViewModel : ObservableObject
{
    private readonly OpdViewModel _opd;
    private readonly PatientsViewModel _patients;
    private readonly SaleViewModel _sale;
    private readonly ProductsViewModel _products;
    private readonly InventoryViewModel _inventory;
    private readonly ReportsViewModel _reports;
    private readonly SettingsViewModel _settings;
    private readonly DiagnosticsViewModel _diagnostics;

    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private string _activeNav = "opd";
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _subtitle = "";

    /// <summary>
    /// Whether the Diagnostics nav item shows at all — an optional module,
    /// off for clinics that have no in-house lab. Read once at startup and
    /// updated live the moment the Features toggle under Settings is saved,
    /// so turning it on never needs a restart.
    /// </summary>
    [ObservableProperty] private bool _diagnosticsEnabled;

    /// <summary>
    /// The clinic's own name, read once at startup and pushed live the moment
    /// the Clinic tab under Settings is saved — the brand shown on the window
    /// title bar and the top of the sidebar, so both read whatever the clinic
    /// actually typed rather than a name baked into the build.
    /// </summary>
    [ObservableProperty] private string _clinicDisplayName = "";

    partial void OnClinicDisplayNameChanged(string value) => OnPropertyChanged(nameof(WindowTitle));

    public string WindowTitle =>
        $"{(string.IsNullOrWhiteSpace(ClinicDisplayName) ? "Sivaayaan HMS" : ClinicDisplayName)} — OPD & Pharmacy";

    /// <summary>
    /// A screen shown over the shell instead of in its own window. Only one can
    /// be open, and it cannot end up behind anything, so it cannot be forgotten.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverlayOpen))]
    [NotifyPropertyChangedFor(nameof(IsShellEnabled))]
    private object? _overlay;

    public bool IsOverlayOpen => Overlay is not null;
    public bool IsShellEnabled => Overlay is null;

    /// <summary>Shown in the foot of the navigation, where it is always to hand.</summary>
    public string Credit => AppInfo.Credit;

    /// <summary>The developer's name on its own, so the sidebar can make just
    /// that part of the credit line a link.</summary>
    public string Developer => AppInfo.Developer;

    /// <summary>Where that link goes.</summary>
    public string DeveloperUrl => AppInfo.DeveloperUrl;
    public string VersionLabel => AppInfo.VersionLabel;

    private readonly SettingsService _settingsService;

    public MainViewModel(
        OpdViewModel opd,
        PatientsViewModel patients,
        SaleViewModel sale,
        ProductsViewModel products,
        InventoryViewModel inventory,
        ReportsViewModel reports,
        SettingsViewModel settings,
        DiagnosticsViewModel diagnostics,
        SettingsService settingsService)
    {
        _opd = opd;
        _patients = patients;
        _sale = sale;
        _products = products;
        _inventory = inventory;
        _reports = reports;
        _settings = settings;
        _diagnostics = diagnostics;
        _settingsService = settingsService;

        GoAsync("opd").Forget("Loading the first page");
        LoadDiagnosticsToggleAsync().Forget("Loading the Diagnostics module toggle");
        LoadClinicDisplayNameAsync().Forget("Loading the clinic name for the title bar");
    }

    private async Task LoadDiagnosticsToggleAsync()
        => DiagnosticsEnabled = (await _settingsService.GetGeneralAsync()).DiagnosticsEnabled;

    private async Task LoadClinicDisplayNameAsync()
        => ClinicDisplayName = (await _settingsService.GetClinicAsync()).Name;

    [RelayCommand]
    private async Task GoAsync(string key)
    {
        // Every screen change is a marker in the log, so a problem can be placed
        // against what the user was looking at when it happened.
        using var log = AppLog.Enter("Shell.Go", $"to={key}");

        IPage page = key switch
        {
            "patients" => _patients,
            "sale" => _sale,
            "products" => _products,
            "inventory" => _inventory,
            "reports" => _reports,
            "settings" => _settings,
            "diagnostics" => _diagnostics,
            _ => _opd
        };

        // Screens revise their own subtitle as the user works — the Inventory
        // subtitle names the selected medicine — so follow it rather than
        // copying it once and going stale.
        if (CurrentPage is INotifyPropertyChanged previous)
            previous.PropertyChanged -= OnPageChanged;

        ActiveNav = key;
        CurrentPage = page;
        Title = page.Title;
        Subtitle = page.Subtitle;

        if (page is INotifyPropertyChanged current)
            current.PropertyChanged += OnPageChanged;

        await page.LoadAsync();
        Subtitle = page.Subtitle;

        log.Ok($"{page.Title} — {page.Subtitle}");
    }

    /// <summary>
    /// Shows a screen over the shell and returns once it has been closed, so
    /// callers read the same way the old ShowDialog did.
    /// </summary>
    public Task ShowOverlayAsync(object page, Action<Action> onRequestClose)
    {
        var closed = new TaskCompletionSource();

        onRequestClose(() =>
        {
            Overlay = null;
            closed.TrySetResult();
        });

        Overlay = page;
        return closed.Task;
    }

    /// <summary>Closes whatever is over the shell, as the Esc key does.</summary>
    public void CloseOverlay()
    {
        if (Overlay is ConsultationViewModel consultation) consultation.CloseCommand.Execute(null);
        else Overlay = null;
    }

    private void OnPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not IPage page) return;

        Title = page.Title;
        Subtitle = page.Subtitle;
    }
}
