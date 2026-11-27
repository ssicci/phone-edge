using System.Diagnostics;
using Microsoft.Win32;

namespace Twinput.App.Core;

/// <param name="CopiedSettings">옛 settings.json 을 새 데이터 폴더로 복사했다.</param>
/// <param name="RemovedOldRun">옛 Run 값(과 StartupApproved 값)을 지웠다.</param>
/// <param name="EnabledAutostart">옛 자동 시작이 켜져 있어서 Twinput 자동 시작을 켰다.</param>
/// <param name="OldInstalled">옛 PhoneEdge 가 설치되어 있다 (Update.exe 가 있음).</param>
public sealed record LegacyMigrationResult(bool CopiedSettings, bool RemovedOldRun, bool EnabledAutostart, bool OldInstalled);

/// <summary>
/// 제품 이름이 PhoneEdge → Twinput 으로 바뀌면서 Velopack 패키지 ID 가 달라져 기존 사용자는 Twinput 을 새로 설치한다.
/// 처음 실행할 때 옛 설치에서 설정을 복사하고 옛 자동 시작 등록을 정리한다. 옛 앱은 건드리지 않는다(제거는 사용자가 한다).
/// 경로와 레지스트리 키는 주입할 수 있어 테스트는 임시 폴더와 HKCU\Software\TwinputTests\... 를 쓴다.
/// </summary>
public sealed class LegacyMigration
{
    /// <summary>옛 제품 이름 (폴더, 프로세스, Run 값 이름). 이 이름은 이전 처리를 위해 일부러 남긴 것이다.</summary>
    public const string OldName = "PhoneEdge";

    private const string DefaultRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string DefaultApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string SettingsFile = "settings.json";

    private readonly string _oldDir, _newDir, _runKey, _approvedKey;
    private readonly Func<bool> _oldRunning;

    /// <summary>옛 설치 폴더 (%LocalAppData%\PhoneEdge).</summary>
    public static string DefaultOldDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), OldName);

    public LegacyMigration(string oldDir, string newDir, string runKey = DefaultRunKey, string approvedKey = DefaultApprovedKey, Func<bool>? oldProcessRunning = null)
    {
        _oldDir = oldDir;
        _newDir = newDir;
        _runKey = runKey;
        _approvedKey = approvedKey;
        _oldRunning = oldProcessRunning ?? (() => Process.GetProcessesByName(OldName).Length > 0);
    }

    /// <summary>옛 PhoneEdge 가 설치되어 있는지 (Velopack 의 Update.exe 로 판단한다).</summary>
    public bool OldInstalled => File.Exists(Path.Combine(_oldDir, "Update.exe"));

    /// <summary>옛 PhoneEdge 프로세스가 지금 실행 중인지 (두 앱이 동시에 입력을 가로채지 않도록 안내하는 데 쓴다).</summary>
    public bool OldRunning
    {
        get { try { return _oldRunning(); } catch { return false; } }
    }

    /// <param name="newAutostart">Twinput 자동 시작 (설치본이 아니면 null: 그러면 Run 값은 건드리지 않는다).</param>
    public LegacyMigrationResult Run(Autostart? newAutostart)
    {
        bool copied = CopySettings();
        bool removed = false, enabled = false;
        if (newAutostart != null)
        {
            // 옛 값이 켜져 있었는지: 값이 있고 Windows 의 "시작 앱"에서 꺼 두지 않았을 때.
            bool wasOn = ReadOldRunOn(out bool present);
            if (present)
            {
                RemoveOldRun();
                removed = true;
                if (wasOn) { newAutostart.Enable(); enabled = true; }
            }
        }
        return new LegacyMigrationResult(copied, removed, enabled, OldInstalled);
    }

    /// <summary>옛 settings.json 을 복사한다. 새 설정이 이미 있거나 옛 설정이 없으면 하지 않는다. 로그, current, packages 는 복사하지 않는다.</summary>
    private bool CopySettings()
    {
        try
        {
            string from = Path.Combine(_oldDir, SettingsFile), to = Path.Combine(_newDir, SettingsFile);
            if (!File.Exists(from) || File.Exists(to)) return false;
            Directory.CreateDirectory(_newDir);
            File.Copy(from, to, overwrite: false);
            return true;
        }
        catch { return false; }   // 이전 실패는 치명적이지 않다 (기본 설정으로 시작)
    }

    private bool ReadOldRunOn(out bool present)
    {
        using var run = Registry.CurrentUser.OpenSubKey(_runKey);
        present = run?.GetValue(OldName) is string s && !string.IsNullOrWhiteSpace(s);
        if (!present) return false;
        using var approved = Registry.CurrentUser.OpenSubKey(_approvedKey);
        bool disabled = approved?.GetValue(OldName) is byte[] { Length: > 0 } data && (data[0] & 1) == 1;
        return !disabled;
    }

    private void RemoveOldRun()
    {
        using (var run = Registry.CurrentUser.OpenSubKey(_runKey, writable: true))
            run?.DeleteValue(OldName, throwOnMissingValue: false);
        using var approved = Registry.CurrentUser.OpenSubKey(_approvedKey, writable: true);
        approved?.DeleteValue(OldName, throwOnMissingValue: false);
    }
}
