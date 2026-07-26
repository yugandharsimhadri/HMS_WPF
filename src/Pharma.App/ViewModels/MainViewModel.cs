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
    private readonly ReportsViewModel _reports;
    private readonly SettingsViewModel _settings;

    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private string _activeNav = "opd";
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _subtitle = "";

    public MainViewModel(
        OpdViewModel opd,
        PatientsViewModel patients,
        SaleViewModel sale,
        ProductsViewModel products,
        ReportsViewModel reports,
        SettingsViewModel settings)
    {
        _opd = opd;
        _patients = patients;
        _sale = sale;
        _products = products;
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
            "reports" => _reports,
            "settings" => _settings,
            _ => _opd
        };

        ActiveNav = key;
        CurrentPage = page;
        Title = page.Title;
        Subtitle = page.Subtitle;

        await page.LoadAsync();
        Subtitle = page.Subtitle;
    }
}
