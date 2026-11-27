using Twinput.Contracts;
using Twinput.Phone.Protocol;
using Twinput.Phone.Tracking;

namespace Twinput.Phone;

/// <summary>
/// 폰 모드 입력 처리 (단일 스레드에서만 호출). 커서 추적과 제어 메시지 생성을 맡고,
/// 바이트 출력과 이탈 통지는 콜백으로 내보낸다. 소켓 없이 테스트할 수 있게 분리했다.
/// </summary>
internal sealed class PhoneInputProcessor(Action<byte[]> send, Action<double> exitRequested, Action<int>? pause = null)
{
    /// <summary>
    /// 구석으로 미는 이동 뒤 세로 위치 이동 전에 쉬는 시간(ms). 폰 포인터 가속이 켜져 있으면 바로 이어 보낸 이동이
    /// 고속으로 판정돼 크게 튀므로(실측) 속도 추정이 식을 때까지 기다린다. 가속이 꺼져 있으면 0.
    /// </summary>
    public int HomeSettleMs { get; set; }

    public const string MouseName = "Twinput Mouse";
    public const string KeyboardName = "Twinput Keyboard";

    public CursorTracker Tracker { get; } = new();
    public bool InPhoneMode { get; private set; }

    private PhoneMouseButtons _buttons;
    private bool _keysDown;

    /// <summary>가상 입력 장치를 만든다 (연결 직후 1회).</summary>
    public void CreateDevices()
    {
        send(ControlMessages.UhidCreate(HidReports.KeyboardId, 0, 0, KeyboardName, HidReports.KeyboardDescriptor));
        send(ControlMessages.UhidCreate(HidReports.MouseId, 0, 0, MouseName, HidReports.MouseDescriptor));
    }

    public void Enter(double ratio, PhoneEntryEdge edge = PhoneEntryEdge.Left)
    {
        InPhoneMode = true;
        _buttons = PhoneMouseButtons.None;
        Home(ratio, edge);
    }

    public void Leave()
    {
        if (!InPhoneMode) return;
        ReleaseAll();
        InPhoneMode = false;
        Tracker.Leave();
    }

    public void Mouse(int dx, int dy, PhoneMouseButtons buttons, int wheel)
    {
        if (!InPhoneMode) return;
        bool held = buttons != PhoneMouseButtons.None;
        if (Tracker.Move(dx, dy, held, out double exitY))
        {
            ReleaseAll();
            InPhoneMode = false;
            exitRequested(exitY);
            return;
        }
        _buttons = buttons;
        foreach (var r in HidReports.MouseChunked(dx, dy, buttons, wheel))
            send(ControlMessages.UhidInput(HidReports.MouseId, r));
    }

    public void Keyboard(byte modifiers, ReadOnlySpan<byte> keys)
    {
        if (!InPhoneMode) return;
        _keysDown = modifiers != 0 || keys.IndexOfAnyExcept((byte)0) >= 0;
        send(ControlMessages.UhidInput(HidReports.KeyboardId, HidReports.Keyboard(modifiers, keys)));
    }

    /// <summary>화면 크기 변경. 폰 모드 중이면 같은 가장자리 비율로 진입 가장자리(4방향)에 다시 기준을 맞춘다.</summary>
    public void Resize(int width, int height)
    {
        if (Tracker.SetScreen(width, height) && InPhoneMode)
            Home(Tracker.EdgeRatio, Tracker.Edge);
    }

    /// <summary>이득 측정 1단계: 폰 모드 밖에서 커서를 좌상단에 붙인다.</summary>
    public void CalibrationHome()
    {
        long home = -(long)Math.Max(Tracker.Width, Tracker.Height) * 20;
        foreach (var r in HidReports.MouseChunked(home, home, PhoneMouseButtons.None, 0))
            send(ControlMessages.UhidInput(HidReports.MouseId, r));
    }

    /// <summary>
    /// 이득 측정 2단계: 오른쪽으로 counts 를 리포트 한 건으로 보낸다. 앞 이동과 충분히 떨어진 단일 리포트에는
    /// 가속이 붙지 않음을 실측으로 확인했다 (가속 켜짐에서도 2.680).
    /// </summary>
    public void CalibrationStep(int counts)
        => send(ControlMessages.UhidInput(HidReports.MouseId, HidReports.Mouse(counts, 0, PhoneMouseButtons.None, 0)));

    /// <summary>연결이 끊겼을 때. 아무것도 보내지 않고 상태만 지운다.</summary>
    public void Reset()
    {
        InPhoneMode = false;
        _buttons = PhoneMouseButtons.None;
        _keysDown = false;
        Tracker.Leave();
    }

    private void Home(double ratio, PhoneEntryEdge edge)
    {
        var moves = Tracker.Enter(ratio, edge);
        for (int i = 0; i < moves.Count; i++)
        {
            if (i > 0 && HomeSettleMs > 0) pause?.Invoke(HomeSettleMs);
            foreach (var r in HidReports.MouseChunked(moves[i].Dx, moves[i].Dy, _buttons, 0))
                send(ControlMessages.UhidInput(HidReports.MouseId, r));
        }
    }

    private void ReleaseAll()
    {
        if (_buttons != PhoneMouseButtons.None)
        {
            send(ControlMessages.UhidInput(HidReports.MouseId, HidReports.Mouse(0, 0, PhoneMouseButtons.None, 0)));
            _buttons = PhoneMouseButtons.None;
        }
        if (_keysDown)
        {
            send(ControlMessages.UhidInput(HidReports.KeyboardId, HidReports.Keyboard(0, [])));
            _keysDown = false;
        }
    }
}
