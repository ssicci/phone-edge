using System.Globalization;
using System.Text.RegularExpressions;
using PhoneEdge.Contracts;
using PhoneEdge.Phone.Adb;

namespace PhoneEdge.Phone.Diagnostics;

/// <summary>`adb devices -l` 한 번의 결과.</summary>
public readonly record struct AdbDevicesResult(int? ExitCode, string Output, string? Error);

/// <summary>폰 찾기 단계 판정. Problem 이 None 이면 Serial 로 연결을 시도한다 (null 이면 잠시 기다린다).</summary>
public readonly record struct DeviceVerdict(PhoneLinkProblem Problem, string? Serial, string? Detail);

/// <summary>adb·USB 관찰 결과 → 진단 (순수 함수, 폰 없이 테스트).</summary>
public static partial class LinkDiagnostics
{
    public static DeviceVerdict ClassifyDevices(AdbDevicesResult adb, IReadOnlyCollection<string> usbDeviceIds)
    {
        // 1) adb 자체가 실행·통신되지 않음
        if (adb.Error != null)
            return new(PhoneLinkProblem.AdbFailed, null, adb.Error);
        if (adb.ExitCode != 0 || AdbErrorRegex().IsMatch(adb.Output))
            return new(PhoneLinkProblem.AdbFailed, null, $"exit {adb.ExitCode}: {adb.Output.Trim()}");

        var devices = AdbParsers.ParseDevices(adb.Output);
        var usable = devices.Where(d => d.State == "device").ToList();
        string list = string.Join(", ", devices.Select(d => d.Serial + "=" + d.State));

        // 2) 쓸 수 있는 기기
        if (usable.Count == 1) return new(PhoneLinkProblem.None, usable[0].Serial, null);
        if (usable.Count > 1) return new(PhoneLinkProblem.MultipleDevices, null, list);

        // 3) adb 에 보이지만 쓸 수 없음
        if (devices.Any(d => d.State == "unauthorized")) return new(PhoneLinkProblem.Unauthorized, null, list);
        if (devices.Any(d => d.State is "authorizing" or "connecting")) return new(PhoneLinkProblem.None, null, list); // 잠깐 지나가는 상태
        if (devices.Count > 0) return new(PhoneLinkProblem.Offline, null, list); // offline, no permissions, recovery 등

        // 4) adb 에 없음: Windows 에 안드로이드 폰이 USB 로 보이면 디버깅 꺼짐, 아니면 기기 없음
        var phones = usbDeviceIds.Where(IsAndroidPhoneUsbDevice).ToList();
        return phones.Count > 0
            ? new(PhoneLinkProblem.DebuggingOff, null, "USB 에만 보임: " + string.Join(", ", phones))
            : new(PhoneLinkProblem.NoDevice, null, null);
    }

    /// <summary>
    /// After the app turned USB debugging off on purpose (PhoneLinkFix.DisableUsbDebugging), the phone shows up on USB
    /// but not in adb, which would otherwise be reported as DebuggingOff. That state is expected, so it is reported
    /// as no problem (so is NoDevice). The flag stays set while adb cannot see the phone (debugging off, cable removed, adb hiccup)
    /// and is cleared as soon as adb sees the phone again in any state (the user turned debugging back on).
    /// </summary>
    public static (DeviceVerdict Report, bool StillIntentionallyOff) ApplyIntentionalDebuggingOff(DeviceVerdict verdict, bool intentionallyOff)
    {
        if (!intentionallyOff) return (verdict, false);
        // NoDevice is hidden too: unplugging after an intentional off is expected, and turning debugging back on
        // re-enumerates USB, which showed a ~2 s NoDevice blip on the reference phone.
        return verdict.Problem switch
        {
            PhoneLinkProblem.DebuggingOff or PhoneLinkProblem.NoDevice => (new DeviceVerdict(PhoneLinkProblem.None, null, null), true),
            PhoneLinkProblem.AdbFailed => (verdict, true),
            _ => (verdict, false),
        };
    }

    /// <summary>`dumpsys input` 에 가상 마우스·키보드가 모두 등록됐는지 (InputReader 의 "Device N: 이름" 줄).</summary>
    public static bool HasVirtualInputDevices(string dumpsysInput, string mouseName, string keyboardName)
        => Regex.IsMatch(dumpsysInput, @"Device -?\d+: " + Regex.Escape(mouseName) + @"\s*$", RegexOptions.Multiline)
        && Regex.IsMatch(dumpsysInput, @"Device -?\d+: " + Regex.Escape(keyboardName) + @"\s*$", RegexOptions.Multiline);

    /// <summary>
    /// USB 인스턴스 ID 가 안드로이드 폰(복합 장치의 부모)인지. 인터페이스·기능 자식(&amp;MI_xx, &amp;MS_COMP_MTP, &amp;MODEM 등 PID 뒤에 더 붙은 것)은 빼서 한 폰이 한 번만 잡히게 한다.
    /// 삼성(04E8)은 SSD·모니터 허브 등 폰 아닌 USB 장치도 많아 폰 PID 대역(0x68xx: MTP/ADB 6860, 테더링 6863 등)만 인정한다.
    /// </summary>
    public static bool IsAndroidPhoneUsbDevice(string instanceId)
    {
        var m = UsbIdRegex().Match(instanceId);
        if (!m.Success || instanceId.Contains("&MI_", StringComparison.OrdinalIgnoreCase)) return false;
        int vid = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int pid = int.Parse(m.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (vid == 0x04E8) return (pid & 0xFF00) == 0x6800;
        return AndroidPhoneVendors.Contains(vid);
    }

    /// <summary>폰을 주로 만드는 제조사 VID (Android 공식 OEM USB 드라이버 목록 기준 일부).</summary>
    private static readonly HashSet<int> AndroidPhoneVendors =
    [
        0x18D1, // Google (Pixel)
        0x22B8, // Motorola
        0x1004, // LG
        0x0BB4, // HTC
        0x2717, // Xiaomi
        0x12D1, // Huawei
        0x2A70, // OnePlus
        0x22D9, // OPPO
        0x2D95, // vivo
        0x0FCE, // Sony Mobile
        0x19D2, // ZTE
        0x1EBF, // Nothing
    ];

    [GeneratedRegex(@"^USB\\VID_([0-9A-F]{4})&PID_([0-9A-F]{4})\\", RegexOptions.IgnoreCase)]
    private static partial Regex UsbIdRegex();

    [GeneratedRegex(@"cannot connect to daemon|failed to start daemon|could not read ok from ADB Server|protocol fault|failed to check server version|adb server version .* doesn't match", RegexOptions.IgnoreCase)]
    private static partial Regex AdbErrorRegex();
}

/// <summary>연결 단계 실패와 그 원인.</summary>
public sealed class LinkFailureException(PhoneLinkProblem problem, string message) : Exception(message)
{
    public PhoneLinkProblem Problem { get; } = problem;
}
