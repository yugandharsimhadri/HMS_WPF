using System.Reflection;

namespace Pharma.App;

/// <summary>
/// Who built this and which build it is.
///
/// One place, so the line in the sidebar and the line at the top of every log
/// file cannot end up disagreeing about the version — which is exactly the sort
/// of thing that wastes an hour on a support call.
/// </summary>
public static class AppInfo
{
    public const string Developer = "Sivayaan Technologies";

    /// <summary>
    /// "1.0.0". The fourth part of an assembly version is always zero here and
    /// says nothing to the person reading it off the screen.
    /// </summary>
    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";

    public static string Credit => $"Developed by {Developer}";

    public static string VersionLabel => $"Version {Version}";
}
