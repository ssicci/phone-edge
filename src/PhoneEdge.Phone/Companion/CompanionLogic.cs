using System.Globalization;
using System.Text.RegularExpressions;
using PhoneEdge.Contracts;

namespace PhoneEdge.Phone.Companion;

public enum InstallOutcome { Success, SignatureMismatch, Failed }

/// <summary>What the phone reported about the installed companion app.</summary>
public readonly record struct InstalledCompanion(bool Installed, long VersionCode, bool CanWriteSecureSettings);

/// <summary>adb output → companion state (pure functions, tested without a phone).</summary>
public static partial class CompanionLogic
{
    public const string Package = "io.github.ssicci.phoneedge.companion";
    public const string WriteSecureSettings = "android.permission.WRITE_SECURE_SETTINGS";
    public const string PostNotifications = "android.permission.POST_NOTIFICATIONS";

    /// <summary>Shell command whose output <see cref="ParseInstalled"/> reads (one adb round trip).</summary>
    public static string QueryCommand(string package) =>
        $"pm list packages --show-versioncode {package}; dumpsys package {package} | grep -m1 '{WriteSecureSettings}: granted='";

    /// <summary>
    /// Parses `pm list packages --show-versioncode pkg` (substring match, so the exact package is picked)
    /// plus the `...WRITE_SECURE_SETTINGS: granted=true|false` line from `dumpsys package pkg`.
    /// </summary>
    public static InstalledCompanion ParseInstalled(string output, string package)
    {
        foreach (Match m in PackageLineRegex().Matches(output))
        {
            if (m.Groups[1].Value != package) continue;
            long version = long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            bool granted = Regex.IsMatch(output, Regex.Escape(WriteSecureSettings) + @": granted=true");
            return new InstalledCompanion(true, version, granted);
        }
        return new InstalledCompanion(false, 0, false);
    }

    /// <summary>
    /// Unknown when the bundled APK could not be read (or the phone was not checked); NotInstalled; NeedsUpdate when the
    /// installed version is older than the bundled one or the settings permission is missing; otherwise Installed.
    /// </summary>
    public static CompanionState Decide(long? bundledVersionCode, InstalledCompanion? installed)
    {
        if (bundledVersionCode == null || installed == null) return CompanionState.Unknown;
        var i = installed.Value;
        if (!i.Installed) return CompanionState.NotInstalled;
        if (i.VersionCode < bundledVersionCode.Value || !i.CanWriteSecureSettings) return CompanionState.NeedsUpdate;
        return CompanionState.Installed;
    }

    /// <summary>`adb install` output: "Success", or "Failure [INSTALL_FAILED_...: ...]" / "adb: failed to install ...".</summary>
    public static InstallOutcome ParseInstall(int exitCode, string output)
    {
        if (output.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE", StringComparison.Ordinal)) return InstallOutcome.SignatureMismatch;
        if (exitCode == 0 && SuccessRegex().IsMatch(output) && !output.Contains("Failure", StringComparison.Ordinal)) return InstallOutcome.Success;
        return InstallOutcome.Failed;
    }

    /// <summary>The INSTALL_FAILED_* / INSTALL_PARSE_FAILED_* code in adb's output, for logs.</summary>
    public static string? FailureCode(string output) => FailureRegex().Match(output) is { Success: true } m ? m.Value : null;

    [GeneratedRegex(@"package:(\S+) versionCode:(\d+)")]
    private static partial Regex PackageLineRegex();

    [GeneratedRegex(@"^Success\s*$", RegexOptions.Multiline)]
    private static partial Regex SuccessRegex();

    [GeneratedRegex(@"INSTALL_(?:PARSE_)?FAILED_[A-Z_]+")]
    private static partial Regex FailureRegex();
}
