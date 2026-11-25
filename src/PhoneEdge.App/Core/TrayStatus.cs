using PhoneEdge.Contracts;

namespace PhoneEdge.App.Core;

public enum TrayIconKind { Disconnected, Ready, Phone, Paused, Problem }

/// <summary>상태와 진단 → 트레이 아이콘 종류와 툴팁. 순수 로직.</summary>
public static class TrayStatus
{
    public static TrayIconKind SelectIcon(bool paused, InputMode mode, PhoneLinkState state, PhoneLinkDiagnosis diagnosis)
    {
        if (paused) return TrayIconKind.Paused;
        if (mode == InputMode.Phone) return TrayIconKind.Phone;
        if (state == PhoneLinkState.Ready) return TrayIconKind.Ready;
        return diagnosis.Problem != PhoneLinkProblem.None ? TrayIconKind.Problem : TrayIconKind.Disconnected;
    }

    public static string Tooltip(TrayIconKind kind, PhoneLinkDiagnosis diagnosis, System.Globalization.CultureInfo? culture = null)
    {
        string text = kind switch
        {
            TrayIconKind.Paused => L.T("Tray_Paused", culture),
            TrayIconKind.Phone => L.T("Tray_Phone", culture),
            TrayIconKind.Ready => L.T(diagnosis.Warnings != PhoneLinkWarnings.None ? "Tray_ReadyWarn" : "Tray_Ready", culture),
            TrayIconKind.Problem => GuidanceTable.For(diagnosis.Problem, culture)?.ShortCause ?? L.T("Tray_Problem", culture),
            _ => L.T("Tray_Disconnected", culture),
        };
        return "PhoneEdge - " + text;   // NotifyIcon.Text 는 127자 이하
    }

    public static bool HasIssue(PhoneLinkDiagnosis d) => d.Problem != PhoneLinkProblem.None || d.Warnings != PhoneLinkWarnings.None;
}

/// <summary>
/// 문제가 일정 시간 지속될 때 문제 종류마다 한 번만 알림을 내보내도록 판단한다.
/// Ready 가 되면 다음 문제는 새 사건으로 보고 다시 알린다.
/// </summary>
public sealed class ProblemNotifier
{
    private readonly TimeSpan _delay;
    private readonly HashSet<PhoneLinkProblem> _notified = new();
    private PhoneLinkProblem _current;
    private DateTime _since;

    public ProblemNotifier(TimeSpan delay) => _delay = delay;

    /// <summary>
    /// 주기적으로 호출한다. 알림을 띄워야 하면 그 문제를, 아니면 null.
    /// 폰이 안 꽂힌 상태(NoDevice)는 평소 상태라 알리지 않고, 연결 시도 하나(꽂음 → 뽑음/Ready)마다 문제별로 한 번씩 알린다.
    /// </summary>
    /// <param name="delay">이번 판단에만 쓸 대기 시간. null 이면 생성자에서 준 값.</param>
    public PhoneLinkProblem? Update(PhoneLinkProblem problem, bool ready, DateTime now, TimeSpan? delay = null)
    {
        if (ready || problem == PhoneLinkProblem.NoDevice) _notified.Clear();
        if (problem != _current)
        {
            _current = problem;
            _since = now;
        }
        if (problem is PhoneLinkProblem.None or PhoneLinkProblem.NoDevice || now - _since < (delay ?? _delay)) return null;
        return _notified.Add(problem) ? problem : null;
    }
}
