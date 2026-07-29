using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pharma.App.ViewModels;
using Pharma.Core.Licensing;

namespace Pharma.App.Views;

/// <summary>
/// What this copy is and how long it may be used for — the screen support asks
/// people to read out.
/// </summary>
public partial class AboutWindow : Window
{
    /// <summary>Creates the dialog, resolving the licence through the container.</summary>
    public AboutWindow()
    {
        InitializeComponent();

        DataContext = new AboutViewModel(App.Services.GetRequiredService<ILicenseService>());
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
