using System.ComponentModel;
using System.Windows;
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
    private readonly DashboardViewModel _dashboard;
    private readonly OpdViewModel _opd;
    private readonly PatientsViewModel _patients;
    private readonly SaleViewModel _sale;
    private readonly ProductsViewModel _products;
    private readonly InventoryViewModel _inventory;
    private readonly ReportsViewModel _reports;
    private readonly SettingsViewModel _settings;
    private readonly DiagnosticsViewModel _diagnostics;
    private readonly AppointmentsViewModel _appointments;
    private readonly PediatricsViewModel _pediatrics;
    private readonly DentistViewModel _dentist;
    private readonly PathologyLabViewModel _pathologyLab;
    private readonly PathologyLabMasterViewModel _pathologyLabMaster;
    private readonly GeneralMasterViewModel _generalMaster;

    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private string _activeNav = "dashboard";
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
    /// Whether the OPD nav item shows at all. Unlike every other module
    /// toggle, this defaults true — every clinic already running this
    /// application is already using the OPD queue — and is only ever off for
    /// a clinic that deliberately switched it off (a dentist- or
    /// pediatrician-only practice). Same live-update shape as
    /// <see cref="DiagnosticsEnabled"/>.
    /// </summary>
    [ObservableProperty] private bool _opdEnabled = true;

    /// <summary>Whether the Pharmacy counter / Medicines / Inventory nav
    /// items show at all. Defaults true for the same reason as
    /// <see cref="OpdEnabled"/>.</summary>
    [ObservableProperty] private bool _pharmacyEnabled = true;

    /// <summary>Whether the Appointments nav item shows at all. Off by
    /// default, same live-update shape as <see cref="DiagnosticsEnabled"/>.</summary>
    [ObservableProperty] private bool _appointmentsEnabled;

    /// <summary>Whether the Pediatrics nav item shows at all. Off by
    /// default, same live-update shape as <see cref="DiagnosticsEnabled"/>.</summary>
    [ObservableProperty] private bool _pediatricsEnabled;

    /// <summary>Whether the Dentist nav item shows at all. Off by default,
    /// same live-update shape as <see cref="DiagnosticsEnabled"/>.</summary>
    [ObservableProperty] private bool _dentistEnabled;

    /// <summary>Whether the Pathology Lab nav item shows at all. Off by
    /// default, same live-update shape as <see cref="DiagnosticsEnabled"/>.</summary>
    [ObservableProperty] private bool _pathologyLabEnabled;

    // ── Collapsible nav groups — Pharmacy and Pathology Lab are the only
    //    two left with children of their own (Pediatrics and Dentist are
    //    flat single destinations again now that their masters live on
    //    General Master); whether a group is open is purely a UI
    //    preference, not persisted, and defaults open so nothing looks
    //    different from before until someone actually collapses one. ─────

    [ObservableProperty] private bool _pharmacyNavExpanded = true;
    [ObservableProperty] private bool _pathologyLabNavExpanded = true;

    [RelayCommand]
    private void TogglePharmacyNav() => PharmacyNavExpanded = !PharmacyNavExpanded;

    [RelayCommand]
    private void TogglePathologyLabNav() => PathologyLabNavExpanded = !PathologyLabNavExpanded;

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

    /// <summary>Who is signed in, for the sidebar's "Logged in as" line. Null
    /// — <see cref="CurrentUserService.IsSignedIn"/> false — whenever login
    /// is switched off, which is unchanged from before login existed.</summary>
    public CurrentUserService CurrentUser { get; }

    private readonly SettingsService _settingsService;
    private readonly AuthService _auth;

    public MainViewModel(
        DashboardViewModel dashboard,
        OpdViewModel opd,
        PatientsViewModel patients,
        SaleViewModel sale,
        ProductsViewModel products,
        InventoryViewModel inventory,
        ReportsViewModel reports,
        SettingsViewModel settings,
        DiagnosticsViewModel diagnostics,
        AppointmentsViewModel appointments,
        PediatricsViewModel pediatrics,
        DentistViewModel dentist,
        PathologyLabViewModel pathologyLab,
        PathologyLabMasterViewModel pathologyLabMaster,
        GeneralMasterViewModel generalMaster,
        SettingsService settingsService,
        CurrentUserService currentUser,
        AuthService auth)
    {
        _dashboard = dashboard;
        _opd = opd;
        _patients = patients;
        _sale = sale;
        _products = products;
        _inventory = inventory;
        _reports = reports;
        _settings = settings;
        _diagnostics = diagnostics;
        _appointments = appointments;
        _pediatrics = pediatrics;
        _dentist = dentist;
        _pathologyLab = pathologyLab;
        _pathologyLabMaster = pathologyLabMaster;
        _generalMaster = generalMaster;
        _settingsService = settingsService;
        CurrentUser = currentUser;
        _auth = auth;

        GoAsync("dashboard").Forget("Loading the first page");
        LoadModuleTogglesAsync().Forget("Loading the module toggles");
        LoadClinicDisplayNameAsync().Forget("Loading the clinic name for the title bar");
    }

    private async Task LoadModuleTogglesAsync()
    {
        var general = await _settingsService.GetGeneralAsync();
        DiagnosticsEnabled = general.DiagnosticsEnabled;
        OpdEnabled = general.OpdEnabled;
        PharmacyEnabled = general.PharmacyEnabled;
        AppointmentsEnabled = general.AppointmentsEnabled;
        PediatricsEnabled = general.PediatricsEnabled;
        DentistEnabled = general.DentistEnabled;
        PathologyLabEnabled = general.PathologyLabEnabled;
    }

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
            "opd" => _opd,
            "patients" => _patients,
            "sale" => _sale,
            "products" => _products,
            "inventory" => _inventory,
            "reports" => _reports,
            "settings" => _settings,
            "diagnostics" => _diagnostics,
            "appointments" => _appointments,
            "pediatrics" => _pediatrics,
            "dentist" => _dentist,
            "pathologylab" => _pathologyLab,
            "pathologylab-master" => _pathologyLabMaster,
            "general-master" => _generalMaster,
            _ => _dashboard
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

    /// <summary>
    /// Reopens the login window to switch users. Cancelling it leaves the
    /// current session exactly as it was — this is a front-desk convenience,
    /// not a lockout, so backing out of it must not sign anyone out.
    /// </summary>
    [RelayCommand]
    private void LogOut()
    {
        var window = new Views.LoginWindow(_auth, _settingsService, allowCancel: true)
            { Owner = Application.Current.MainWindow };

        if (window.ShowDialog() == true && window.LoggedInUser is { } user)
            CurrentUser.SignIn(user);
    }

    private void OnPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not IPage page) return;

        Title = page.Title;
        Subtitle = page.Subtitle;
    }
}
