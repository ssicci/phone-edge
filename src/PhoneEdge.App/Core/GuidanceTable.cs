using System.Globalization;
using PhoneEdge.Contracts;

namespace PhoneEdge.App.Core;

/// <summary>문제 하나에 대한 사용자 안내. 문구는 리소스(Guide_*)에 있다.</summary>
/// <param name="ShortCause">툴팁에 쓰는 짧은 원인.</param>
/// <param name="Cause">안내 창의 원인 한 줄.</param>
/// <param name="Steps">번호를 붙여 보여 줄 해결 단계.</param>
/// <param name="CanRetry">"다시 시도" 버튼으로 도움이 되는 문제인지.</param>
public sealed record Guidance(string ShortCause, string Cause, string[] Steps, bool CanRetry);

public static class GuidanceTable
{
    /// <summary>문제 → 리소스 키 접두사. 문제 없음(None)은 없다.</summary>
    private static readonly Dictionary<PhoneLinkProblem, string> Keys = new()
    {
        [PhoneLinkProblem.NoDevice] = "Guide_NoDevice",
        [PhoneLinkProblem.DebuggingOff] = "Guide_DebuggingOff",
        [PhoneLinkProblem.Unauthorized] = "Guide_Unauthorized",
        [PhoneLinkProblem.Offline] = "Guide_Offline",
        [PhoneLinkProblem.MultipleDevices] = "Guide_MultipleDevices",
        [PhoneLinkProblem.AdbFailed] = "Guide_AdbFailed",
        [PhoneLinkProblem.ServerFailed] = "Guide_Server",
        [PhoneLinkProblem.InputDeviceFailed] = "Guide_Server",
    };

    /// <summary>문제 없음(None)에는 null. culture 를 생략하면 현재 앱 언어.</summary>
    public static Guidance? For(PhoneLinkProblem problem, CultureInfo? culture = null)
    {
        if (!Keys.TryGetValue(problem, out var k)) return null;
        string[] steps = L.T(k + "_Steps", culture).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return new Guidance(L.T(k + "_Short", culture), L.T(k + "_Cause", culture), steps, true);
    }

    public static string PointerAccelerationCause(CultureInfo? culture = null) => L.T("Guide_AccelCause", culture);
    public static string PointerAccelerationManual(CultureInfo? culture = null) => L.T("Guide_AccelManual", culture);
}
