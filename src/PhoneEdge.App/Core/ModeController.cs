using PhoneEdge.Contracts;

namespace PhoneEdge.App.Core;

public enum InputMode { Laptop, Phone }

/// <summary>화면(모니터) 사각형. Right/Bottom 은 포함하지 않는 값.</summary>
public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Height => Bottom - Top;
}

/// <summary>노트북 커서를 움직이는 쪽 (Win32 구현은 Native 에 있다).</summary>
public interface ICursorPlacer
{
    /// <summary>화면 좌표 (x, y) 로 커서를 옮긴다.</summary>
    void PlaceAt(int x, int y);
}

/// <summary>
/// 노트북 모드 ↔ 폰 모드 상태 기계. Win32 에 의존하지 않는다.
/// 모든 메서드는 UI(훅) 스레드에서 호출한다고 가정한다. 링크 이벤트는 호출자가 UI 스레드로 옮긴다.
/// 입력 메서드의 bool 반환값은 "Windows 로 전달하지 말고 삼킬 것"이다.
/// </summary>
public sealed class ModeController
{
    private readonly IPhoneLink _link;
    private readonly ICursorPlacer _cursor;
    private readonly KeyboardState _phoneKeys = new();      // 폰으로 보낸 키 상태
    private readonly KeyboardState _physicalKeys = new();   // 실제로 눌려 있는 키 (모드와 무관, 비상 단축키 판별용)
    private readonly HashSet<int> _phoneDownVks = new();    // 폰 모드에서 눌려 Windows 가 모르는 키
    private readonly HashSet<int> _windowsDownVks = new();  // 노트북 모드에서 눌려 Windows 가 아는 키
    private PhoneMouseButtons _phoneButtons, _windowsButtons, _swallowedUp;
    private double _lastYRatio;
    private ResolvedSlot? _slot;                     // 지금 모니터 구성의 폰 자리 (null 이면 진입하지 않음)
    private ResolvedSlot _entrySlot;                 // 이번 폰 모드에 들어갈 때의 폰 자리 (복귀 위치 기준)
    private double _pushed;                          // 경계에 닿은 뒤 밀어낸 거리 (오진입 방지)
    private long _lastPushTick;
    private readonly Func<long> _clock;
    /// <summary>복귀할 때 커서를 경계에서 이만큼 안쪽에 놓는다 (바로 다시 진입하지 않도록).</summary>
    public const int ReturnInset = 8;
    private bool _enabled = true;

    public ModeController(IPhoneLink link, ICursorPlacer cursor, EntryGuard? guard = null, Func<long>? clock = null)
    {
        _link = link;
        _cursor = cursor;
        Guard = guard ?? EntryGuard.Default;
        _clock = clock ?? (() => Environment.TickCount64);
    }

    /// <summary>오진입 방지 값.</summary>
    public EntryGuard Guard { get; set; }

    public InputMode Mode { get; private set; } = InputMode.Laptop;
    /// <summary>모드가 바뀔 때 발생 (UI 갱신용).</summary>
    public event Action<InputMode>? ModeChanged;

    /// <summary>
    /// 지금 모니터 구성의 폰 자리. 폰 모드 중에 바꾸면 먼저 노트북으로 복귀한다(복귀 위치는 들어갈 때의 폰 자리).
    /// </summary>
    public ResolvedSlot? Slot
    {
        get => _slot;
        set
        {
            if (_slot == value) return;
            EmergencyExit();
            _slot = value;
            _pushed = 0;
        }
    }

    /// <summary>false(일시 정지)면 폰 모드에 들어가지 않고, 폰 모드였다면 즉시 복귀한다.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value) EmergencyExit();
        }
    }

    /// <summary>
    /// Raw Input 마우스 이동 한 건 (dx, dy 는 원시 카운트, cursor 는 이동 직전의 화면 좌표).
    /// 폰 자리의 경계를 바깥으로 밀어 오진입 방지를 통과하면 폰 모드로 들어간다.
    /// </summary>
    public void OnRawMove(int dx, int dy, int cursorX, int cursorY)
    {
        if (Mode == InputMode.Phone)
        {
            _link.SendMouse(dx, dy, _phoneButtons, 0);
            return;
        }

        if (!_enabled || _slot is not { } s) { _pushed = 0; return; }
        var m = s.Monitor;
        bool vertical = s.Side is PhoneSide.Right or PhoneSide.Left;
        int along = vertical ? cursorY : cursorX;
        bool onEdge = s.Side switch
        {
            PhoneSide.Right => cursorX >= m.Right - 1,
            PhoneSide.Left => cursorX <= m.Left,
            PhoneSide.Top => cursorY <= m.Top,
            _ => cursorY >= m.Bottom - 1,
        };
        int corner = (int)Math.Round(Guard.CornerRatio * s.SpanLength);
        bool inSpan = along >= s.SpanStart + corner && along < s.SpanEnd - corner;
        if (!onEdge || !inSpan) { _pushed = 0; return; }

        int outward = s.Side switch { PhoneSide.Right => dx, PhoneSide.Left => -dx, PhoneSide.Top => -dy, _ => dy };
        if (outward < 0) { _pushed = 0; return; }   // 반대로 움직이면 처음부터
        if (outward == 0) return;                   // 변을 따라 미끄러지는 움직임은 누적을 그대로 둔다

        long now = _clock();
        if (now - _lastPushTick > Guard.IdleResetMs) _pushed = 0;
        _lastPushTick = now;
        _pushed += outward;

        if (_pushed >= Guard.PushCounts
            && _windowsButtons == PhoneMouseButtons.None
            && _link.State == PhoneLinkState.Ready)
        {
            double ratio = s.SpanLength <= 1 ? 0 : (double)(along - s.SpanStart) / (s.SpanLength - 1);
            Enter(s, Math.Clamp(ratio, 0.0, 1.0));
        }
    }

    /// <summary>LL 마우스 훅의 WM_MOUSEMOVE. 폰 모드에서는 커서를 움직이지 않도록 삼킨다.</summary>
    public bool OnHookMouseMove() => Mode == InputMode.Phone;

    public bool OnMouseButton(PhoneMouseButtons button, bool down)
    {
        if (Mode == InputMode.Phone)
        {
            if (!down && (_windowsButtons & button) != 0)
            {
                _windowsButtons &= ~button;   // 노트북 모드에서 누른 버튼의 뗌은 Windows 로 보낸다
                return false;
            }
            _phoneButtons = down ? _phoneButtons | button : _phoneButtons & ~button;
            _link.SendMouse(0, 0, _phoneButtons, 0);
            return true;
        }

        if (down)
        {
            _windowsButtons |= button;
            return false;
        }
        if ((_swallowedUp & button) != 0)
        {
            _swallowedUp &= ~button;          // 폰 모드에서 누른 버튼의 뗌은 Windows 가 모른다
            return true;
        }
        _windowsButtons &= ~button;
        return false;
    }

    public bool OnWheel(int notches)
    {
        if (Mode != InputMode.Phone) return false;
        if (notches != 0) _link.SendMouse(0, 0, _phoneButtons, notches);
        return true;
    }

    /// <summary>키 입력 한 건. vkCode 는 키 식별(눌림/뗌 짝 맞추기)에, 스캔코드는 HID 변환에 쓴다.</summary>
    public bool OnKey(int vkCode, int scanCode, bool extended, bool down)
    {
        byte usage = HidKeyMap.Resolve(vkCode, scanCode, extended);
        _physicalKeys.Set(usage, down);

        if (Mode == InputMode.Phone)
        {
            if (!down && _windowsDownVks.Remove(vkCode)) return false;   // Windows 쪽에서 눌린 키의 뗌은 통과

            if (down && usage == HidKeyMap.Escape
                && (_physicalKeys.Modifiers & 0x11) != 0 && (_physicalKeys.Modifiers & 0x44) != 0)
            {
                _phoneDownVks.Add(vkCode);   // Esc 의 뗌은 삼킨다
                EmergencyExit();
                return true;
            }

            if (down) _phoneDownVks.Add(vkCode); else _phoneDownVks.Remove(vkCode);
            if (usage != 0 && _phoneKeys.Set(usage, down))
                _link.SendKeyboard(_phoneKeys.Modifiers, _phoneKeys.Keys);
            return true;
        }

        if (down)
        {
            _windowsDownVks.Add(vkCode);
            return false;
        }
        if (_phoneDownVks.Remove(vkCode)) return true;   // 폰 모드에서 눌린 키의 뗌은 Windows 가 모른다
        _windowsDownVks.Remove(vkCode);
        return false;
    }

    /// <summary>링크 상태 변화. Ready 가 아니게 되면 비상 복귀.</summary>
    public void OnLinkStateChanged(PhoneLinkState state)
    {
        if (state != PhoneLinkState.Ready) EmergencyExit();
    }

    /// <summary>폰 커서가 진입 경계를 넘음. 노트북 커서를 우측 경계 안쪽 같은 높이에 놓는다.</summary>
    public void OnExitRequested(double yRatio)
    {
        if (Mode != InputMode.Phone) return;
        LeavePhoneMode();
        PlaceCursor(Math.Clamp(yRatio, 0.0, 1.0));
    }

    /// <summary>들어갈 때의 폰 자리에서 같은 비율 위치, 모니터 안쪽 <see cref="ReturnInset"/> 에 커서를 놓는다.</summary>
    private void PlaceCursor(double ratio)
    {
        var s = _entrySlot;
        var m = s.Monitor;
        int along = s.SpanStart + (int)Math.Round(ratio * Math.Max(0, s.SpanLength - 1));
        switch (s.Side)
        {
            case PhoneSide.Right: _cursor.PlaceAt(m.Right - 1 - ReturnInset, along); break;
            case PhoneSide.Left: _cursor.PlaceAt(m.Left + ReturnInset, along); break;
            case PhoneSide.Top: _cursor.PlaceAt(along, m.Top + ReturnInset); break;
            default: _cursor.PlaceAt(along, m.Bottom - 1 - ReturnInset); break;
        }
    }

    /// <summary>비상 복귀: 폰 커서 위치와 무관하게 즉시 노트북 모드로. 폰 모드가 아니면 아무 일도 없다.</summary>
    public void EmergencyExit()
    {
        if (Mode != InputMode.Phone) return;
        LeavePhoneMode();
        PlaceCursor(_lastYRatio);
    }

    private void Enter(ResolvedSlot slot, double ratio)
    {
        _entrySlot = slot;
        _lastYRatio = ratio;
        _pushed = 0;
        _phoneKeys.Clear();
        _phoneButtons = PhoneMouseButtons.None;
        _phoneDownVks.Clear();
        _swallowedUp = PhoneMouseButtons.None;
        Mode = InputMode.Phone;
        _link.Enter(ratio, PhoneSides.EntryEdge(slot.Side));   // 비율은 폰 자리 구간 안의 상대 위치 = 진입 경계를 따른 위치
        ModeChanged?.Invoke(Mode);
    }

    private void LeavePhoneMode()
    {
        Mode = InputMode.Laptop;
        _swallowedUp = _phoneButtons;   // 폰 모드에서 눌린 채인 버튼의 뗌은 삼킨다
        _phoneButtons = PhoneMouseButtons.None;
        _phoneKeys.Clear();
        try { _link.Leave(); }
        finally { ModeChanged?.Invoke(Mode); }
    }
}
