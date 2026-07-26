using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private string _activeNav = "opd";
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _subtitle = "";

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

    public MainViewModel(
        OpdViewModel opd,
        PatientsViewModel patients,
        SaleViewModel sale,
        ProductsViewModel products,
        InventoryViewModel inventory,
        ReportsViewModel reports,
        SettingsViewModel settings)
    {
        _opd = opd;
        _patients = patients;
        _sale = sale;
        _products = products;
        _inventory = inventory;
        _reports = reports;
        _settings = settings;

        GoAsync("opd").Forget("Loading the first page");
    }

    [RelayCommand]
    private async Task GoAsync(string key)
    {
        IPage page = key switch
        {
            "patients" => _patients,
            "sale" => _sale,
            "products" => _products,
            "inventory" => _inventory,
            "reports" => _reports,
            "settings" => _settings,
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
