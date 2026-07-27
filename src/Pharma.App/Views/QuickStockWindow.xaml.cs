using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pharma.App.ViewModels;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.Views;

/// <summary>
/// The counter's own way of putting stock on the shelf, without leaving the
/// bill being served. Deliberately small: the operator has a patient waiting.
/// </summary>
public partial class QuickStockWindow : Window
{
    /// <summary>True once stock has gone on the shelf, so the caller can refresh.</summary>
    public bool Added { get; private set; }

    public QuickStockWindow(Product product)
    {
        InitializeComponent();

        var vm = new QuickStockViewModel(App.Services.GetRequiredService<PharmacyService>(), product);

        vm.Added += () =>
        {
            Added = true;
            Close();
        };

        DataContext = vm;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
