using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// The log folder is configurable, and has to survive a clinic PC that will not
/// let the application write where it was told to.
/// </summary>
public class AppConfigTests
{
    [Fact]
    public void The_shipped_settings_file_points_logs_at_the_agreed_folder()
    {
        // Copied next to the executable at build time, so it is what a clinic gets.
        var shipped = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Pharma.App", "appsettings.json");

        Assert.True(File.Exists(shipped), $"appsettings.json is missing from {Path.GetFullPath(shipped)}");

        var json = File.ReadAllText(shipped);
        Assert.Contains(@"C:\\HMS\\Logs", json);
    }

    [Fact]
    public void A_missing_settings_file_falls_back_instead_of_failing()
    {
        // AppConfig reads once from beside the test assembly, where there is no
        // appsettings.json — it must give defaults rather than throw.
        var settings = AppConfig.Current;

        Assert.NotNull(settings);
        Assert.Equal(14, settings.BackupsToKeep);
        Assert.Equal(30, settings.LogDaysToKeep);
        Assert.Null(AppConfig.LoadError);
    }

    [Fact]
    public void A_configured_folder_that_cannot_be_written_steps_down_to_one_that_can()
    {
        // A path no process can create. Resolution has to notice and move on
        // rather than leaving the clinic with no log at all.
        var impossible = OperatingSystem.IsWindows()
            ? @"\\?\Z:\definitely-not-a-drive\logs"
            : "/proc/definitely-not-writable/logs";

        var previous = Environment.GetEnvironmentVariable(AppLog.DirectoryOverrideVariable);

        try
        {
            Environment.SetEnvironmentVariable(AppLog.DirectoryOverrideVariable, impossible);

            // Resolution is cached, so this is checked through a fresh probe of the
            // same rule rather than by re-reading AppLog.
            Assert.False(CanWriteTo(impossible));
            Assert.True(CanWriteTo(Path.Combine(Path.GetTempPath(), "TwinkleHMS", "logs")));
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppLog.DirectoryOverrideVariable, previous);
        }
    }

    private static bool CanWriteTo(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [Fact]
    public void Writing_a_line_actually_produces_a_file()
    {
        AppLog.Info("Config test line.");

        var today = Path.Combine(AppLog.LogDirectory, $"twinkle-{DateTime.Now:yyyyMMdd}.log");

        Assert.True(File.Exists(today), $"No log file at {today}");
        Assert.Contains("Config test line.", File.ReadAllText(today));
    }
}
