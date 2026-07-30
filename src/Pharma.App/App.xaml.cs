using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.App.ViewModels;
using Pharma.Core.Licensing;
using Pharma.Data;
using Pharma.Data.Licensing;

namespace Pharma.App;

public partial class App : Application
{
    public const string ProductName = "Twinkle Children's Hospital";

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        HookGlobalExceptionHandlers();

        // Before any window exists, so every text box in the application is
        // covered including the ones inside dialogs.
        SelectOnFocus.Register();

        // Community licence: free for organisations under ~$1M USD annual revenue,
        // which covers a single clinic. Required before any PDF is generated.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        AppLog.Info($"---- {ProductName} starting (v{AppInfo.Version}, {AppInfo.Developer}) ----");
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

        // Licensing. Each piece is registered against its interface, so the day
        // a signed licence file or an activation server arrives, only the
        // provider line changes â€” nothing that consumes ILicenseService moves.
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<ILicenseStore, LicenseStorage>();
        services.AddSingleton<ILicenseProvider, EmbeddedEvaluationLicenseProvider>();
        services.AddSingleton<ILicenseService, LicenseService>();

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
        // Before anything else opens. The database is not touched, the theme is
        // not read and no window is shown until this copy is entitled to run â€”
        // a licence check that happens after the user is already working is a
        // licence check nobody trusts.
        if (!LicenceAllowsStartup()) return;

        try
        {
            await DbBootstrapper.InitialiseAsync(Services.GetRequiredService<IDbContextFactory<AppDbContext>>());
            AppLog.Info("Database ready.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Database could not be opened.", ex);
            Dialog.Show(
                $"The database could not be opened.\n\n{DbBootstrapper.DatabasePath}\n\n{ex.Message}" +
                $"\n\nDetails were written to:\n{AppLog.CurrentFile}",
                ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // Before the window is shown, so it never appears in the wrong theme and
        // then repaints in front of the user.
        try
        {
            AppTheme.Apply((await Services.GetRequiredService<SettingsService>().GetAsync()).Theme);
        }
        catch (Exception ex)
        {
            AppLog.Error("The theme could not be read; carrying on with the light one.", ex);
        }

        try
        {
            var window = new MainWindow { DataContext = Services.GetRequiredService<MainViewModel>() };
            window.Show();
            AppLog.Info("Main window shown.");

            StartLicenceWatch();

            CheckDataHealthAsync(window).Forget("Checking the data at startup");
        }
        catch (Exception ex)
        {
            AppLog.Error("Main window failed to open.", ex);
            Dialog.Show(
                $"The application could not start.\n\n{ex.Message}\n\nDetails were written to:\n{AppLog.CurrentFile}",
                ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// The startup gate: may this copy run at all?
    /// </summary>
    /// <returns><see langword="true"/> to carry on starting. When it returns
    /// false the reason has been shown and shutdown is already under way.</returns>
    private bool LicenceAllowsStartup()
    {
        try
        {
            var result = Services.GetRequiredService<ILicenseService>().Validate();
            if (result.IsValid) return true;

            Dialog.Show(result.Message, ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(2);
            return false;
        }
        catch (Exception ex)
        {
            // A licence check that throws must not be a way in. It refuses,
            // says so plainly, and leaves the detail in the log.
            AppLog.Error("Licence: validation failed unexpectedly.", ex);

            Dialog.Show(LicenseConstants.UnreadableMessage, ProductName,
                        MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(2);
            return false;
        }
    }

    /// <summary>
    /// Re-checks the licence while the application is open, so a copy left
    /// running for weeks does not outlive its evaluation.
    /// </summary>
    /// <remarks>
    /// The application is not closed from under whoever is using it. Billing a
    /// patient is a poor moment to lose a window, and there is no global
    /// autosave to fall back on â€” every screen saves on its own explicit
    /// action. So the user is told what has happened and the application closes
    /// when they acknowledge it, which gives them the chance to finish and save
    /// the one thing they had open.
    /// </remarks>
    private void StartLicenceWatch()
    {
        var licence = Services.GetRequiredService<ILicenseService>();

        var timer = new DispatcherTimer { Interval = LicenseConstants.ValidationInterval };

        timer.Tick += (_, _) =>
        {
            LicenseValidationResult result;

            try
            {
                result = licence.Validate();
            }
            catch (Exception ex)
            {
                // Unlike the startup gate, a failure here leaves the clinic
                // working. Log it and try again at the next tick rather than
                // closing a window over something that may be transient.
                AppLog.Error("Licence: periodic validation failed.", ex);
                return;
            }

            if (result.IsValid) return;

            timer.Stop();

            AppLog.Warn($"Licence: became invalid while running ({result.Status}). Closing.");

            var message = result.Status == LicenseStatus.Expired
                ? LicenseConstants.ExpiredWhileRunningMessage
                : result.Message;

            Dialog.Show(message, ProductName, MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(2);
        };

        timer.Start();

        AppLog.Info(
            $"Licence: re-checking every {LicenseConstants.ValidationInterval.TotalMinutes:N0} minutes.");
    }

    /// <summary>
    /// Says so, once, when the medicine records cannot be right â€” a pack size
    /// that disagrees with its units-per-pack sells strips to people asking for
    /// tablets and reports no error, so something has to raise it.
    ///
    /// Never blocks the desk: the window is already up, and this only offers.
    /// </summary>
    private static async Task CheckDataHealthAsync(Window owner)
    {
        try
        {
            var summary = await Services.GetRequiredService<DataHealthService>().DailyCheckAsync();
            if (summary is null) return;

            var answer = Dialog.Show(
                $"Some medicine records need attention:\n\n{summary}.\n\n" +
                $"Until they are put right, prices and stock counts for those medicines " +
                $"will be wrong.\n\nOpen the data health check now?",
                ProductName, MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes) return;

            new Views.DataHealthWindow { Owner = owner }.ShowDialog();
        }
        catch (Exception ex)
        {
            // A failed health check must never be why the clinic cannot open.
            AppLog.Error("The startup data health check failed.", ex);
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
            Dialog.Show(
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
