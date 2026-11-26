using System.Globalization;
using System.Text.RegularExpressions;

namespace PhoneEdge.Phone.Adb;

public readonly record struct AdbDevice(string Serial, string State);

/// <summary>adb 출력 파서 (폰 없이 테스트 가능).</summary>
public static partial class AdbParsers
{
    /// <summary>`adb devices` 출력 → (serial, state). state: device / unauthorized / offline ...</summary>
    public static List<AdbDevice> ParseDevices(string output)
    {
        var list = new List<AdbDevice>();
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("List of devices", StringComparison.Ordinal) || line.StartsWith('*'))
                continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) list.Add(new AdbDevice(parts[0], parts[1]));
        }
        return list;
    }

    /// <summary>
    /// 폰 현재 화면(논리 디스플레이 0)의 회전 반영 크기를 찾는다.
    /// 지원 형식:
    ///  - `dumpsys window displays` 의 "cur=1080x2520" (회전 반영)
    ///  - `dumpsys display` 의 DisplayInfo "real 1080 x 2520" (logicalWidth/Height, 회전 반영)
    /// </summary>
    public static bool TryParseDisplaySize(string text, out int width, out int height)
    {
        var m = CurRegex().Match(text);
        if (!m.Success) m = RealRegex().Match(text);
        if (m.Success)
        {
            width = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            height = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            return width > 0 && height > 0;
        }
        width = height = 0;
        return false;
    }

    /// <summary>
    /// `dumpsys input` 의 MouseCursorController viewport 줄 → 마우스 커서가 클램프되는 화면.
    /// 폴드7(Android 17)은 커버(displayId 1)와 메인(displayId 0)이 별도 논리 디스플레이라
    /// "마우스 커서가 실제로 있는 화면"을 이 줄로 판단한다.
    /// 예: "viewport: Viewport INTERNAL: displayId=0, ... logicalFrame=[0, 0, 1968, 2184], ..."
    /// </summary>
    public static bool TryParseMouseViewport(string text, out int displayId, out int width, out int height)
    {
        var m = ViewportRegex().Match(text);
        displayId = width = height = 0;
        if (!m.Success) return false;
        displayId = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        int l = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        int t = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        width = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) - l;
        height = int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture) - t;
        return width > 0 && height > 0;
    }

    /// <summary>
    /// `dumpsys input` 의 마우스 호버 위치. 예: "hoveringPointers=[Pointer(id=0, MOUSE) at (267.969, 2183)]"
    /// </summary>
    public static bool TryParseMousePosition(string text, out double x, out double y)
    {
        var m = HoverRegex().Match(text);
        x = y = 0;
        if (!m.Success) return false;
        x = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        y = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>마우스 viewport 줄의 회전값 (0~3, 90° 단위). 없으면 null.</summary>
    public static int? ParseMouseOrientation(string text)
    {
        var m = ViewportOrientationRegex().Match(text);
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// 호버 위치(회전 전 자연 방향 좌표)를 화면 좌표로 바꾼다. width/height 는 viewport 의 논리(회전 후) 크기.
    /// 연속 좌표 변환이라 "크기 − 1" 이 아니라 "크기 − 값" 이다. 폴드7 가로 화면 실측:
    /// orientation 3 은 화면 (2519, 217) 이 (217, 1), orientation 1 은 화면 좌상단 (0, 0) 이 (1080, 0) 으로 보인다. 2 는 대칭 추정.
    /// </summary>
    public static (double X, double Y) HoverToScreen(double x, double y, int orientation, int width, int height) => orientation switch
    {
        1 => (y, height - x),
        2 => (width - x, height - y),
        3 => (width - y, x),
        _ => (x, y),
    };

    [GeneratedRegex(@"viewport: Viewport \w+: displayId=-?\d+,.*?orientation=(\d)")]
    private static partial Regex ViewportOrientationRegex();

    /// <summary>`dumpsys input` 의 "Mouse pointer acceleration enabled: true|false" (Android 16+). 없으면 null.</summary>
    public static bool? ParseMouseAccelerationEnabled(string text)
    {
        var m = AccelRegex().Match(text);
        return m.Success ? m.Groups[1].Value == "true" : null;
    }

    [GeneratedRegex(@"Mouse pointer acceleration enabled: (true|false)")]
    private static partial Regex AccelRegex();

    [GeneratedRegex(@"viewport: Viewport \w+: displayId=(-?\d+),.*?logicalFrame=\[(-?\d+), (-?\d+), (-?\d+), (-?\d+)\]")]
    private static partial Regex ViewportRegex();

    [GeneratedRegex(@"hoveringPointers=\[Pointer\(id=\d+, MOUSE\) at \((-?[\d.]+), (-?[\d.]+)\)")]
    private static partial Regex HoverRegex();

    [GeneratedRegex(@"\bcur=(\d+)x(\d+)")]
    private static partial Regex CurRegex();

    [GeneratedRegex(@"\breal (\d+) x (\d+)")]
    private static partial Regex RealRegex();
}
