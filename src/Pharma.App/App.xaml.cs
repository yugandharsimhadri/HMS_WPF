using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.App.ViewModels;
using Pharma.Data;

namespace Pharma.App;

public partial class App : Application
{
    public const string ProductName = "Twinkle Children's Hospital";

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        HookGlobalExceptionHandlers();

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        AppLog.Info($"---- {ProductName} starting (v{version}) ----");
        AppLog.Info($"Settings: {AppConfig.FilePath}");
        AppLog.Info($"Database: {DbBootstrapper.DatabasePath}");
        AppLog.Info($"Logs:     {AppLog.LogDirectory}");
        AppLog.Info($"Method tracing: {(AppConfig.Current.TraceMethods ? "on" : "off")}");

        // Both of these are the sort of thing that is invisible until someone
        // goes looking for a log that is not where they expected.
        if (AppConfig.LoadError is { } configError) AppLog.Warn(configError);
        if (AppLog.FallbackReason is { } fallback) AppLog.Warn(fallback);

        Services = BuildServices();

        // Startup work is awaited on the dispatcher rather than in an `async void`
        // override, so a failure here is caught instead of taking the process down.
        Dispatcher.InvokeAsync(StartAsync);
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // A context factory rather than a scoped DbContext: WPF has no request
        // scope, so each operation opens and disposes its own short-lived context.
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(DbBootstrapper.ConnectionString));

        services.AddSingleton<OpdService>();
        services.AddSingleton<PharmacyService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<DataHealthService>();
        services.AddSingleton<Pharma.Data.Import.PurchaseImportService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<OpdViewModel>();
        services.AddSingleton<PatientsViewModel>();
        services.AddSingleton<SaleViewModel>();
        services.AddSingleton<ProductsViewModel>();
        services.AddSingleton<InventoryViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddSingleton<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    private async Task StartAsync()
    {
        try
        {
            await DbBootstrapper.InitialiseAsync(Services.GetRequiredService<IDbContextFactory<AppDbContext>>());
            AppLog.Info("Database ready.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Database could not be opened.", ex);
            MessageBox.Show(
                $"The database could not be opened.\n\n{DbBootstrapper.DatabasePath}\n\n{ex.Message}" +
                $"\n\nDetails were written to:\n{AppLog.CurrentFile}",
                ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        try
        {
            var window = new MainWindow { DataContext = Services.GetRequiredService<MainViewModel>() };
            window.Show();
            AppLog.Info("Main window shown.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Main window failed to open.", ex);
            MessageBox.Show(
                $"The application could not start.\n\n{ex.Message}\n\nDetails were written to:\n{AppLog.CurrentFile}",
                ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void HookGlobalExceptionHandlers()
    {
        // Anything thrown on the UI thread: log it, tell the user plainly, and keep
        // the app open. Losing a half-typed bill to a stack trace is worse than the bug.
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Unhandled UI exception.", args.Exception);
            ShowFailure(args.Exception);
            args.Handled = true;
        };

        // A faulted task nobody awaited.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Unobserved task exception.", args.Exception?.GetBaseException());
            args.SetObserved();
        };

        // Last resort: the process is going down regardless, so just record why.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("Fatal exception.", args.ExceptionObject as Exception);

        Exit += (_, args) => AppLog.Info($"---- Exiting (code {args.ApplicationExitCode}) ----");
    }

    private static void ShowFailure(Exception ex)
    {
        try
        {
            MessageBox.Show(
                $"Something went wrong and the last action was cancelled.\n\n{ex.Message}\n\n" +
                $"Your saved data is safe. Details were written to:\n{AppLog.CurrentFile}",
                ProductName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception)
        {
            // If even the dialog fails there is nothing further to do.
        }
    }
}
