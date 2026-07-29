using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Pharma.Core.Licensing;

namespace Pharma.Data.Licensing;

/// <summary>
/// Remembers the last successful run in
/// <c>%ProgramData%\Sivayaan\HMS\runtime.dat</c>.
/// </summary>
/// <remarks>
/// <para>
/// ProgramData rather than Program Files: a standard Windows user cannot write
/// under Program Files, so a state file there would fail to save on every
/// machine where the clinic staff are not administrators — which is all of the
/// ones that are set up properly.
/// </para>
/// <para>
/// The record carries a SHA-256 of its own contents and a salt, so an edit with
/// Notepad is detected rather than believed. That salt is in the executable and
/// can be recovered from it: this stops casual editing, not a determined
/// attacker. See the security review in the licensing documentation.
/// </para>
/// </remarks>
public sealed class LicenseStorage : ILicenseStore
{
    /// <summary>Mixed into the signature so the file cannot be rewritten with a
    /// plain hash of its own contents.</summary>
    private const string IntegritySalt = "Sivayaan.HMS.Runtime.v1";

    private readonly string _path;

    /// <summary>Creates a store at the default location under %ProgramData%.</summary>
    public LicenseStorage() : this(DefaultDirectory) { }

    /// <summary>Creates a store in a given directory. Used by the tests.</summary>
    /// <param name="directory">Folder to hold the state file.</param>
    public LicenseStorage(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, LicenseConstants.RuntimeStateFileName);
    }

    /// <summary>Where the state file lives on a real installation.</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        LicenseConstants.StorageVendorFolder,
        LicenseConstants.StorageProductFolder);

    /// <inheritdoc />
    public LastRunRecord Read()
    {
        try
        {
            if (!File.Exists(_path)) return LastRunRecord.None;

            var parts = File.ReadAllText(_path).Split('|');
            if (parts.Length != 2) return LastRunRecord.Tampered;

            if (!Sign(parts[0]).Equals(parts[1].Trim(), StringComparison.OrdinalIgnoreCase))
            {
                AppLog.Warn("Licence: the runtime record did not match its signature.");
                return LastRunRecord.Tampered;
            }

            if (!DateTime.TryParse(parts[0], CultureInfo.InvariantCulture,
                                   DateTimeStyles.RoundtripKind, out var lastRun))
            {
                return LastRunRecord.Tampered;
            }

            return new LastRunRecord(lastRun.ToUniversalTime(), false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A state file that cannot be read must not stop a clinic opening.
            // It reads as "never run", which is the forgiving answer.
            AppLog.Warn($"Licence: the runtime record could not be read ({ex.Message}).");
            return LastRunRecord.None;
        }
    }

    /// <inheritdoc />
    public void Write(DateTime utcNow)
    {
        try
        {
            var stamp = utcNow.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            File.WriteAllText(_path, $"{stamp}|{Sign(stamp)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing the record weakens rollback detection until the next
            // successful write; refusing to open the clinic over it would be
            // the wrong trade.
            AppLog.Warn($"Licence: the runtime record could not be written ({ex.Message}).");
        }
    }

    private static string Sign(string payload)
        => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payload + IntegritySalt)));
}
