using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pharma.App.ViewModels;
using Pharma.Data;

namespace Pharma.App.Views;

/// <summary>
/// Shows what is wrong with the medicine records and puts it right in one pass.
/// Opened from Settings, and offered at startup when there is something to fix.
/// </summary>
public partial class DataHealthWindow : Window
{
    public DataHealthWindow()
    {
        InitializeComponent();

        var vm = new DataHealthViewModel(App.Services.GetRequiredService<DataHealthService>());
        DataContext = vm;

        Loaded += (_, _) => vm.LoadAsync().Forget("Checking the data");
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
