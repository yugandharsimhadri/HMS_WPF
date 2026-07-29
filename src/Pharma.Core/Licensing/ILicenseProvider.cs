namespace Pharma.Core.Licensing;

/// <summary>
/// Where a licence comes from.
/// </summary>
/// <remarks>
/// This is the seam the rest of the application is built against. Today the
/// only implementation carries an evaluation licence inside the executable; a
/// later <c>JsonLicenseProvider</c> reading a signed file, or an
/// <c>OnlineLicenseProvider</c> calling an activation server, replaces it by
/// changing one registration in the container and nothing else.
///
/// An implementation reports facts only. Whether those facts mean the
/// application may run is <see cref="ILicenseService"/>'s decision, so that
/// expiry, rollback detection and logging are not reimplemented per source.
/// </remarks>
public interface ILicenseProvider
{
    /// <summary>
    /// Reads the licence.
    /// </summary>
    /// <returns>The licence, or <see langword="null"/> if none could be read —
    /// a missing file, a bad signature, an unreachable server. A null is not an
    /// error to be thrown at the user; the service turns it into
    /// <see cref="LicenseStatus.Unreadable"/>.</returns>
    LicenseDescriptor? TryGetLicense();
}
