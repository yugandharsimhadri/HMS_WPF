using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.App.ViewModels;
using Pharma.Data;
using Pharma.Data.Import;

namespace Pharma.App.Views;

public partial class ImportWindow : Window
{
    private readonly ImportViewModel _vm;

    /// <summary>Raised after a successful import so the caller can refresh its stock list.</summary>
    public bool Imported { get; private set; }

    public ImportWindow()
    {
        InitializeComponent();

        _vm = new ImportViewModel(
            App.Services.GetRequiredService<PurchaseImportService>(),
            App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>());

        _vm.Imported += () => Imported = true;
        DataContext = _vm;

        Loaded += (_, _) => _vm.LoadAsync().Forget("Loading import profiles");
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
