using System.Text.Json;

namespace Pharma.Data;

/// <summary>Settings read from appsettings.json beside the executable.</summary>
public class AppSettings
{
    /// <summary>Where the daily log files are written.</summary>
    public string? LogDirectory { get; set; }

    /// <summary>Full path to the database file. Null uses the ProgramData default.</summary>
    public string? DatabasePath { get; set; }

    /// <summary>Daily backups to keep before the oldest is deleted.</summary>
    public int BackupsToKeep { get; set; } = 14;

    /// <summary>Days of log files to keep.</summary>
    public int LogDaysToKeep { get; set; } = 30;

    /// <summary>
    /// Writes a line on entry to and exit from every service and command, with
    /// the identifiers needed to trace a problem back to the record it touched.
    /// On by default: a clinic PC has no debugger attached and the log is the
    /// only account of what happened. Set false to quieten it.
    /// </summary>
    public bool TraceMethods { get; set; } = true;
}

/// <summary>
/// Machine-level configuration, read once from a plain JSON file next to the
/// executable so a clinic can move the log or database folder without a rebuild.
///
/// Deliberately not stored in the database: logging has to work when the database
/// is the thing that failed.
/// </summary>
public static class AppConfig
{
    public const string FileName = "appsettings.json";

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    private static AppSettings? _settings;

    public static AppSettings Current => _settings ??= Load();

    /// <summary>Set when the file existed but could not be read, so startup can say so.</summary>
    public static string? LoadError { get; private set; }

    private static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();

            var json = File.ReadAllText(FilePath);

            return JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            // A broken config file must not stop the clinic opening — fall back to
            // the built-in defaults and say so once the log is running.
            LoadError = $"{FileName} could not be read ({ex.Message}). Built-in defaults are being used.";
            return new AppSettings();
        }
    }

    /// <summary>Used by tests to point the application somewhere harmless.</summary>
    public static void Override(AppSettings settings) => _settings = settings;
}
