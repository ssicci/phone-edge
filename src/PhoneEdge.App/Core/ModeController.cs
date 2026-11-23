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
    /// <summary>우측 경계 안쪽, 높이 비율 yRatio 위치에 커서를 놓는다.</summary>
    void PlaceAtRightEdge(ScreenRect monitor, double yRatio);
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
    private ScreenRect _entryMonitor;
    private double _lastYRatio;
    private bool _enabled = true;

    public ModeController(IPhoneLink link, ICursorPlacer cursor)
    {
        _link = link;
        _cursor = cursor;
    }

    public InputMode Mode { get; private set; } = InputMode.Laptop;

    /// <summary>모드가 바뀔 때 발생 (UI 갱신용).</summary>
    public event Action<InputMode>? ModeChanged;

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
    /// Raw Input 마우스 이동 한 건. rightEdgeMonitor: 커서가 있는 모니터가 가장 오른쪽 모니터일 때 그 사각형, 아니면 null.
    /// </summary>
    public void OnRawMove(int dx, int dy, int cursorX, int cursorY, ScreenRect? rightEdgeMonitor)
    {
        if (Mode == InputMode.Phone)
        {
            _link.SendMouse(dx, dy, _phoneButtons, 0);
            return;
        }

        if (_enabled && dx > 0 && rightEdgeMonitor is { } m
            && cursorX >= m.Right - 1
            && _windowsButtons == PhoneMouseButtons.None
            && _link.State == PhoneLinkState.Ready)
        {
            double y = m.Height <= 1 ? 0 : (double)(cursorY - m.Top) / (m.Height - 1);
            Enter(m, Math.Clamp(y, 0.0, 1.0));
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
        _cursor.PlaceAtRightEdge(_entryMonitor, Math.Clamp(yRatio, 0.0, 1.0));
    }

    /// <summary>비상 복귀: 폰 커서 위치와 무관하게 즉시 노트북 모드로. 폰 모드가 아니면 아무 일도 없다.</summary>
    public void EmergencyExit()
    {
        if (Mode != InputMode.Phone) return;
        LeavePhoneMode();
        _cursor.PlaceAtRightEdge(_entryMonitor, _lastYRatio);
    }

    private void Enter(ScreenRect monitor, double yRatio)
    {
        _entryMonitor = monitor;
        _lastYRatio = yRatio;
        _phoneKeys.Clear();
        _phoneButtons = PhoneMouseButtons.None;
        _phoneDownVks.Clear();
        _swallowedUp = PhoneMouseButtons.None;
        Mode = InputMode.Phone;
        _link.Enter(yRatio);
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
