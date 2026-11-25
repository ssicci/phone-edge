using System.Text;
using PhoneEdge.Contracts;

namespace PhoneEdge.App;

/// <summary>
/// 개발용 IPhoneLink. 가상 폰 화면 안에서 커서를 누적하고, 좌측 경계를 넘으면 ExitRequested 를 발생시킨다.
/// 받은 입력은 로그로 남긴다.
/// </summary>
public sealed class FakePhoneLink : IPhoneLink
{
    public const int ScreenWidth = 1080, ScreenHeight = 2520;

    private readonly Action<string> _log;
    private readonly object _gate = new();
    private double _x, _y;
    private bool _inPhone;
    private PhoneLinkState _state = PhoneLinkState.Disconnected;

    public FakePhoneLink(Action<string> log) => _log = log;

    public PhoneLinkState State => _state;
    public event Action<PhoneLinkState>? StateChanged;
    public event Action<double>? ExitRequested;

    public PhoneLinkDiagnosis Diagnosis { get; private set; } = PhoneLinkDiagnosis.Healthy;
    public event Action<PhoneLinkDiagnosis>? DiagnosisChanged;

    /// <summary>개발 메뉴용: 문제를 흉내 낸다. 문제가 있으면 Disconnected, 없으면 Ready.</summary>
    public void SetDiagnosis(PhoneLinkProblem problem, PhoneLinkWarnings warnings)
    {
        var d = new PhoneLinkDiagnosis(problem, warnings, problem == PhoneLinkProblem.None ? null : $"[fake] 흉내 낸 문제: {problem}\nadb devices -l\n(출력 없음)");
        Diagnosis = d;
        _log($"[fake] 진단 → {problem} / {warnings}");
        DiagnosisChanged?.Invoke(d);
        SetConnected(problem == PhoneLinkProblem.None);
    }

    public async Task<bool> TryFixAsync(PhoneLinkFix fix)
    {
        _log($"[fake] 자동 조치 {fix}");
        await Task.Delay(500);
        if (fix == PhoneLinkFix.DisableUsbDebugging) return true;   // 종료할 때 호출된다. 흉내만 내고 상태는 바꾸지 않는다
        if (fix == PhoneLinkFix.Retry) SetDiagnosis(PhoneLinkProblem.None, Diagnosis.Warnings);
        else SetDiagnosis(Diagnosis.Problem, Diagnosis.Warnings & ~PhoneLinkWarnings.PointerAcceleration);
        return true;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        SetConnected(true);
        return Task.CompletedTask;
    }

    /// <summary>트레이 메뉴에서 연결 끊김/복구를 흉내 낸다 (비상 복귀 확인용).</summary>
    public void SetConnected(bool connected)
    {
        var next = connected ? PhoneLinkState.Ready : PhoneLinkState.Disconnected;
        lock (_gate)
        {
            if (_state == next) return;
            _state = next;
            if (!connected) _inPhone = false;
        }
        _log($"[fake] 상태 → {next}");
        StateChanged?.Invoke(next);
    }

    public void Enter(double yRatio)
    {
        lock (_gate)
        {
            if (_state != PhoneLinkState.Ready) return;
            _inPhone = true;
            _x = 0;
            _y = Math.Clamp(yRatio, 0, 1) * (ScreenHeight - 1);
            _log($"[fake] Enter yRatio={yRatio:F3} → 폰 커서 ({_x:F0},{_y:F0})");
        }
    }

    public void Leave()
    {
        lock (_gate)
        {
            if (!_inPhone) return;
            _inPhone = false;
            _log("[fake] Leave (버튼·키 모두 뗌)");
        }
    }

    public void SendMouse(int dx, int dy, PhoneMouseButtons buttons, int wheel)
    {
        double? exitY = null;
        lock (_gate)
        {
            if (_state != PhoneLinkState.Ready || !_inPhone) return;
            _x = Math.Min(_x + dx, ScreenWidth - 1);
            _y = Math.Clamp(_y + dy, 0, ScreenHeight - 1);
            _log($"[fake] 마우스 dx={dx} dy={dy} buttons={buttons} wheel={wheel} → ({_x:F0},{_y:F0})");
            if (_x < 0)
            {
                _inPhone = false;
                exitY = _y / (ScreenHeight - 1);
            }
        }
        if (exitY is { } y)
        {
            _log($"[fake] 좌측 경계 넘음 → ExitRequested({y:F3})");
            ExitRequested?.Invoke(y);
        }
    }

    public void SendKeyboard(byte modifiers, ReadOnlySpan<byte> keys)
    {
        lock (_gate)
        {
            if (_state != PhoneLinkState.Ready || !_inPhone) return;
        }
        var sb = new StringBuilder("[fake] 키보드 mod=0x").Append(modifiers.ToString("X2")).Append(" keys=[");
        for (int i = 0; i < keys.Length; i++) sb.Append(i > 0 ? " " : "").Append("0x").Append(keys[i].ToString("X2"));
        _log(sb.Append(']').ToString());
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
