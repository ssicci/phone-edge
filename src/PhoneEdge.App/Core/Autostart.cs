using Microsoft.Win32;

namespace PhoneEdge.App.Core;

public enum AutostartState
{
    /// <summary>Run 값이 없다.</summary>
    Off,
    /// <summary>Run 값이 우리 실행기를 가리키고 Windows 에서도 꺼져 있지 않다.</summary>
    On,
    /// <summary>Run 값은 있지만 다른 경로를 가리킨다 (옛 설치 위치 등).</summary>
    OtherPath,
    /// <summary>사용자가 Windows 설정·작업 관리자의 "시작 앱"에서 껐다 (StartupApproved).</summary>
    DisabledInWindows,
}

/// <summary>
/// Windows 시작 시 자동 실행. HKCU\...\Run 의 "PhoneEdge" 값에 설치 루트의 Velopack 실행기 경로(따옴표 포함)를 둔다.
/// current\PhoneEdge.exe 가 아니라 실행기를 써야 업데이트로 current 폴더가 바뀌어도 경로가 유지된다.
/// 사용자가 "시작 앱"에서 끄면 Windows 가 ...\Explorer\StartupApproved\Run 에 이진값을 쓰는데,
/// 첫 바이트가 홀수(보통 03)면 사용 안 함이다 (공식 문서는 없고 널리 알려진 동작이다).
/// 레지스트리 키 경로는 주입할 수 있어서 테스트는 HKCU\Software\PhoneEdgeTests\... 를 쓴다.
/// </summary>
public sealed class Autostart
{
    public const string ValueName = "PhoneEdge";
    private const string DefaultRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string DefaultApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private readonly string _runKey, _approvedKey, _launcherPath;

    /// <param name="launcherPath">설치 루트의 실행기(PhoneEdge.exe) 전체 경로.</param>
    public Autostart(string launcherPath, string runKey = DefaultRunKey, string approvedKey = DefaultApprovedKey)
    {
        _launcherPath = launcherPath;
        _runKey = runKey;
        _approvedKey = approvedKey;
    }

    public static string Quote(string path) => "\"" + path + "\"";

    /// <summary>Run 값(따옴표·인수 제거)이 우리 실행기를 가리키는지.</summary>
    private bool PointsToLauncher(string value)
    {
        string v = value.Trim();
        if (v.StartsWith('"'))
        {
            int end = v.IndexOf('"', 1);
            v = end > 0 ? v[1..end] : v[1..];
        }
        else
        {
            v = v.Split(' ', 2)[0];
        }
        return string.Equals(Path.GetFullPath(v), Path.GetFullPath(_launcherPath), StringComparison.OrdinalIgnoreCase);
    }

    public AutostartState GetState()
    {
        using var run = Registry.CurrentUser.OpenSubKey(_runKey);
        if (run?.GetValue(ValueName) is not string value || string.IsNullOrWhiteSpace(value)) return AutostartState.Off;
        bool mine;
        try { mine = PointsToLauncher(value); } catch { mine = false; }
        if (!mine) return AutostartState.OtherPath;
        return IsDisabledInWindows() ? AutostartState.DisabledInWindows : AutostartState.On;
    }

    public bool IsEnabled => GetState() == AutostartState.On;

    private bool IsDisabledInWindows()
    {
        using var approved = Registry.CurrentUser.OpenSubKey(_approvedKey);
        return approved?.GetValue(ValueName) is byte[] { Length: > 0 } data && (data[0] & 1) == 1;
    }

    /// <summary>켜기: Run 값을 쓰고, 사용 안 함 기록이 있으면 지운다.</summary>
    public void Enable()
    {
        using (var run = Registry.CurrentUser.CreateSubKey(_runKey))
            run.SetValue(ValueName, Quote(_launcherPath), RegistryValueKind.String);
        ClearApproved();
    }

    /// <summary>끄기: Run 값을 지운다.</summary>
    public void Disable()
    {
        using var run = Registry.CurrentUser.OpenSubKey(_runKey, writable: true);
        run?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>제거 정리: Run 과 StartupApproved 값을 모두 지운다.</summary>
    public void Remove()
    {
        Disable();
        ClearApproved();
    }

    private void ClearApproved()
    {
        using var approved = Registry.CurrentUser.OpenSubKey(_approvedKey, writable: true);
        approved?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
