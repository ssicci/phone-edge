using System.IO.Compression;
using Twinput.Contracts;
using Twinput.Phone.Companion;

namespace Twinput.Phone.Tests;

public class CompanionTests
{
    private const string Pkg = CompanionLogic.Package;
    private static readonly string Fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "companion-AndroidManifest.axml");

    // ---- binary manifest ----

    [Fact]
    public void Reads_package_and_version_from_real_binary_manifest()
    {
        // Fixture: AndroidManifest.xml taken from an AGP 9.5 debug build of the companion (versionName 0.4.0).
        var info = ApkManifestReader.TryParseManifest(File.ReadAllBytes(Fixture));
        Assert.NotNull(info);
        Assert.Equal(Pkg, info.Value.Package);
        Assert.Equal("0.4.0", info.Value.VersionName);
        Assert.Equal(400, info.Value.VersionCode); // X*10000 + Y*100 + Z
    }

    [Fact]
    public void Reads_from_apk_zip()
    {
        string apk = Path.Combine(Path.GetTempPath(), "twinput-test-" + Guid.NewGuid() + ".apk");
        try
        {
            using (var zip = ZipFile.Open(apk, ZipArchiveMode.Create))
            {
                var e = zip.CreateEntry("AndroidManifest.xml");
                using var s = e.Open();
                s.Write(File.ReadAllBytes(Fixture));
            }
            Assert.Equal(Pkg, ApkManifestReader.TryRead(apk)?.Package);
        }
        finally { File.Delete(apk); }
    }

    [Fact]
    public void Garbage_or_missing_file_gives_null()
    {
        Assert.Null(ApkManifestReader.TryParseManifest([1, 2, 3, 4, 5, 6, 7, 8, 9]));
        Assert.Null(ApkManifestReader.TryParseManifest("<manifest package=\"x\"/>"u8));
        Assert.Null(ApkManifestReader.TryRead(Path.Combine(Path.GetTempPath(), "no-such-" + Guid.NewGuid() + ".apk")));
    }

    // ---- phone output ----

    private const string Installed =
        "package:io.github.ssicci.phoneedge.companion versionCode:1\n" +
        "      android.permission.WRITE_SECURE_SETTINGS: granted=true\n";

    [Fact]
    public void Parses_installed_version_and_permission()
        => Assert.Equal(new InstalledCompanion(true, 1, true), CompanionLogic.ParseInstalled(Installed, Pkg));

    [Fact]
    public void Missing_grant_line_means_no_permission()
        => Assert.Equal(new InstalledCompanion(true, 1, false),
            CompanionLogic.ParseInstalled("package:io.github.ssicci.phoneedge.companion versionCode:1\n", Pkg));

    [Fact]
    public void Denied_permission()
        => Assert.False(CompanionLogic.ParseInstalled(Installed.Replace("granted=true", "granted=false"), Pkg).CanWriteSecureSettings);

    [Fact]
    public void Picks_exact_package_from_substring_matches()
    {
        const string output = "package:io.github.ssicci.phoneedge.companion.debugtools versionCode:9\n";
        Assert.False(CompanionLogic.ParseInstalled(output, Pkg).Installed);
    }

    [Fact]
    public void Empty_output_means_not_installed()
        => Assert.False(CompanionLogic.ParseInstalled("", Pkg).Installed);

    // ---- state ----

    [Theory]
    [InlineData(null, true, 400, true, CompanionState.Unknown)]       // no bundled APK
    [InlineData(400L, false, 0, false, CompanionState.NotInstalled)]
    [InlineData(400L, true, 1, true, CompanionState.NeedsUpdate)]     // older
    [InlineData(400L, true, 400, false, CompanionState.NeedsUpdate)]  // permission missing
    [InlineData(400L, true, 400, true, CompanionState.Installed)]
    [InlineData(400L, true, 500, true, CompanionState.Installed)]     // newer than bundled
    public void Decide(long? bundled, bool installed, long version, bool granted, CompanionState expected)
        => Assert.Equal(expected, CompanionLogic.Decide(bundled, new InstalledCompanion(installed, version, granted)));

    [Fact]
    public void Unchecked_phone_is_unknown() => Assert.Equal(CompanionState.Unknown, CompanionLogic.Decide(400, null));

    // ---- adb install ----

    [Theory]
    [InlineData(0, "Performing Streamed Install\r\nSuccess\r\n", InstallOutcome.Success)]
    [InlineData(1, "Performing Streamed Install\nadb: failed to install app.apk: Failure [INSTALL_FAILED_UPDATE_INCOMPATIBLE: Existing package io.github.ssicci.phoneedge.companion signatures do not match newer version; ignoring!]\n", InstallOutcome.SignatureMismatch)]
    [InlineData(1, "adb: failed to install app.apk: Failure [INSTALL_FAILED_VERSION_DOWNGRADE: Downgrade detected]\n", InstallOutcome.Failed)]
    [InlineData(1, "adb: failed to install app.apk: Failure [INSTALL_PARSE_FAILED_NO_CERTIFICATES: no certs]\n", InstallOutcome.Failed)]
    [InlineData(0, "Performing Streamed Install\n", InstallOutcome.Failed)]
    public void ParseInstall(int code, string output, InstallOutcome expected)
        => Assert.Equal(expected, CompanionLogic.ParseInstall(code, output));

    [Fact]
    public void FailureCode_extracts_code()
        => Assert.Equal("INSTALL_FAILED_VERSION_DOWNGRADE",
            CompanionLogic.FailureCode("Failure [INSTALL_FAILED_VERSION_DOWNGRADE: Downgrade detected]"));
}
