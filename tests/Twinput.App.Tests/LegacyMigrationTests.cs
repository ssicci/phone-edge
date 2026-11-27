using Microsoft.Win32;
using Twinput.App.Core;
using Xunit;

namespace Twinput.App.Tests;

/// <summary>
/// 옛 PhoneEdge 설치에서 이전. 실제 %LocalAppData% 와 Run 키가 아니라 임시 폴더와
/// HKCU\Software\TwinputTests\{guid} 아래의 테스트 키만 쓴다.
/// </summary>
public sealed class LegacyMigrationTests : IDisposable
{
    private const string Launcher = @"C:\Users\someone\AppData\Local\Twinput\Twinput.exe";
    private const string Old = LegacyMigration.OldName;
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "twinput-test-" + Guid.NewGuid().ToString("N"));
    private readonly string _root = @"Software\TwinputTests\" + Guid.NewGuid().ToString("N");
    private string OldDir => Path.Combine(_tmp, "old");
    private string NewDir => Path.Combine(_tmp, "new");
    private string RunKey => _root + @"\Run";
    private string ApprovedKey => _root + @"\StartupApproved\Run";

    private LegacyMigration Make(bool running = false) => new(OldDir, NewDir, RunKey, ApprovedKey, () => running);
    private Autostart NewAutostart() => new(Launcher, RunKey, ApprovedKey);

    public LegacyMigrationTests() => Directory.CreateDirectory(OldDir);

    public void Dispose()
    {
        if (Directory.Exists(_tmp)) Directory.Delete(_tmp, true);
        Registry.CurrentUser.DeleteSubKeyTree(_root, throwOnMissingSubKey: false);
        try
        {
            using var parent = Registry.CurrentUser.OpenSubKey(@"Software\TwinputTests");
            if (parent is { SubKeyCount: 0, ValueCount: 0 }) Registry.CurrentUser.DeleteSubKey(@"Software\TwinputTests", throwOnMissingSubKey: false);
        }
        catch { }
    }

    private void SetOldRun(string value = "\"C:\\Users\\someone\\AppData\\Local\\PhoneEdge\\PhoneEdge.exe\"")
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        k.SetValue(Old, value);
    }

    private void SetOldApproved(byte first)
    {
        using var k = Registry.CurrentUser.CreateSubKey(ApprovedKey);
        var data = new byte[12];
        data[0] = first;
        k.SetValue(Old, data, RegistryValueKind.Binary);
    }

    private bool Has(string key, string name)
    {
        using var k = Registry.CurrentUser.OpenSubKey(key);
        return k?.GetValue(name) != null;
    }

    // ---- 설정 복사 ----

    [Fact]
    public void CopiesSettingsIncludingProfilesAndKeepsTheOldFile()
    {
        File.WriteAllText(Path.Combine(OldDir, "settings.json"),
            "{\"Language\":\"ko\",\"CompanionPresent\":true,\"DisableUsbDebuggingOnExit\":false,"
            + "\"Profiles\":[{\"Key\":\"k1\",\"Slot\":{\"MonitorId\":\"M1\",\"Side\":\"Top\",\"Start\":0.1,\"End\":0.9}}]}");
        var r = Make().Run(null);
        Assert.True(r.CopiedSettings);
        var store = new SettingsStore(Path.Combine(NewDir, "settings.json"));
        Assert.Equal("ko", store.Language);
        Assert.True(store.CompanionPresent);
        Assert.False(store.DisableUsbDebuggingOnExit);
        var p = Assert.Single(store.Profiles);
        Assert.Equal((PhoneSide.Top, "M1"), (p.Slot.Side, p.Slot.MonitorId));
        Assert.True(File.Exists(Path.Combine(OldDir, "settings.json")));   // 옛 파일은 그대로 (복사)
    }

    [Fact]
    public void SkipsWhenTwinputAlreadyHasSettings()
    {
        File.WriteAllText(Path.Combine(OldDir, "settings.json"), "{\"Language\":\"ko\"}");
        Directory.CreateDirectory(NewDir);
        File.WriteAllText(Path.Combine(NewDir, "settings.json"), "{\"Language\":\"en\"}");
        Assert.False(Make().Run(null).CopiedSettings);
        Assert.Equal("en", new SettingsStore(Path.Combine(NewDir, "settings.json")).Language);
    }

    [Fact]
    public void DoesNothingWithoutOldSettings()
    {
        Assert.False(Make().Run(null).CopiedSettings);
        Assert.False(File.Exists(Path.Combine(NewDir, "settings.json")));
    }

    [Fact]
    public void CopiesOnlySettingsNotLogsOrInstallFolders()
    {
        File.WriteAllText(Path.Combine(OldDir, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(OldDir, "phoneedge.log"), "log");
        File.WriteAllText(Path.Combine(OldDir, "toast-icon.png"), "png");
        Directory.CreateDirectory(Path.Combine(OldDir, "current"));
        Directory.CreateDirectory(Path.Combine(OldDir, "packages"));
        File.WriteAllText(Path.Combine(OldDir, "current", "PhoneEdge.exe"), "x");
        Make().Run(null);
        Assert.Equal(new[] { "settings.json" }, Directory.GetFileSystemEntries(NewDir).Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void RunningTwiceDoesNotOverwriteLaterChanges()
    {
        File.WriteAllText(Path.Combine(OldDir, "settings.json"), "{\"Language\":\"ko\"}");
        Make().Run(null);
        new SettingsStore(Path.Combine(NewDir, "settings.json")).Language = "en";   // 사용자가 Twinput 에서 바꿈
        Assert.False(Make().Run(null).CopiedSettings);
        Assert.Equal("en", new SettingsStore(Path.Combine(NewDir, "settings.json")).Language);
    }

    // ---- 자동 시작 ----

    [Fact]
    public void EnabledOldAutostartIsRemovedAndTwinputAutostartTurnedOn()
    {
        SetOldRun();
        var auto = NewAutostart();
        var r = Make().Run(auto);
        Assert.True(r.RemovedOldRun);
        Assert.True(r.EnabledAutostart);
        Assert.False(Has(RunKey, Old));
        Assert.Equal(AutostartState.On, auto.GetState());
    }

    [Fact]
    public void OldAutostartTurnedOffInWindowsIsRemovedButNotCarriedOver()
    {
        SetOldRun();
        SetOldApproved(0x03);   // 사용자가 Windows 설정에서 껐다
        var auto = NewAutostart();
        var r = Make().Run(auto);
        Assert.True(r.RemovedOldRun);
        Assert.False(r.EnabledAutostart);
        Assert.False(Has(RunKey, Old));
        Assert.False(Has(ApprovedKey, Old));
        Assert.Equal(AutostartState.Off, auto.GetState());
    }

    [Fact]
    public void NoOldAutostartLeavesTwinputAutostartAlone()
    {
        var auto = NewAutostart();
        var r = Make().Run(auto);
        Assert.False(r.RemovedOldRun);
        Assert.False(r.EnabledAutostart);
        Assert.Equal(AutostartState.Off, auto.GetState());
    }

    [Fact]
    public void WithoutTwinputAutostartTheRunKeyIsNotTouched()
    {
        SetOldRun();   // 개발 실행: 설치본이 아니라 Run 값을 건드리지 않는다
        var r = Make().Run(null);
        Assert.False(r.RemovedOldRun);
        Assert.True(Has(RunKey, Old));
    }

    [Fact]
    public void OnlyThePhoneEdgeValueIsRemoved()
    {
        SetOldRun();
        using (var k = Registry.CurrentUser.CreateSubKey(RunKey)) k.SetValue("SomethingElse", "x");
        Make().Run(NewAutostart());
        Assert.True(Has(RunKey, "SomethingElse"));
    }

    // ---- 옛 설치·프로세스 감지 ----

    [Fact]
    public void DetectsAnOldInstallByUpdateExeAndTheRunningProcessByInjection()
    {
        Assert.False(Make().OldInstalled);
        File.WriteAllText(Path.Combine(OldDir, "Update.exe"), "x");
        Assert.True(Make().OldInstalled);
        Assert.False(Make(running: false).OldRunning);
        Assert.True(Make(running: true).OldRunning);
        Assert.True(Make().Run(null).OldInstalled);
    }

    [Fact]
    public void NoticeFlagIsSavedSoTheNoticeShowsOnce()
    {
        var store = new SettingsStore(Path.Combine(NewDir, "settings.json"));
        Assert.False(store.LegacyNoticeShown);
        store.LegacyNoticeShown = true;
        Assert.True(new SettingsStore(Path.Combine(NewDir, "settings.json")).LegacyNoticeShown);
    }

    [Fact]
    public void NoticeTextsExistInBothLanguagesAndNameTheOldApp()
    {
        foreach (var lang in new[] { "en", "ko" })
        {
            var culture = L.CultureFor(lang);
            Assert.Contains("PhoneEdge", L.T("Legacy_Notice", culture));
            Assert.Contains("Twinput", L.T("Legacy_Notice", culture));
            Assert.Contains("PhoneEdge", L.T("Legacy_Running", culture));
        }
    }
}
