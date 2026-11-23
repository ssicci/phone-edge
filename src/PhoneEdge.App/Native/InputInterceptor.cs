using System.Runtime.InteropServices;
using PhoneEdge.App.Core;
using PhoneEdge.Contracts;
using static PhoneEdge.App.Native.NativeMethods;

namespace PhoneEdge.App.Native;

/// <summary>
/// 저수준 훅(WH_MOUSE_LL, WH_KEYBOARD_LL)과 Raw Input 을 걸어 ModeController 에 넘긴다.
/// 훅은 UI 스레드에서 설치한다. 콜백은 ModeController 호출만 하고 즉시 반환한다.
/// 어떤 예외가 나도 비상 복귀 후 입력을 통과시킨다 (사용자가 갇히지 않게).
/// </summary>
internal sealed class InputInterceptor : IDisposable
{
    private readonly ModeController _controller;
    private readonly Func<int, int, ScreenRect?> _rightEdgeMonitor;
    private readonly HookProc _mouseProc, _keyProc;   // GC 로부터 보호하려고 필드로 유지
    private readonly RawWindow _window;
    private IntPtr _mouseHook, _keyHook;
    private int _wheelRemainder;
    private bool _disposed;

    public InputInterceptor(ModeController controller, Func<int, int, ScreenRect?> rightEdgeMonitor)
    {
        _controller = controller;
        _rightEdgeMonitor = rightEdgeMonitor;
        _mouseProc = MouseProc;
        _keyProc = KeyProc;

        _window = new RawWindow(OnRawInput);
        var rid = new[] { new RAWINPUTDEVICE { UsagePage = 0x01, Usage = 0x02, Flags = RIDEV_INPUTSINK, Target = _window.Handle } };
        if (!RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
        {
            int err = Marshal.GetLastWin32Error();
            Dispose();
            throw new InvalidOperationException("Raw Input 등록 실패: " + err);
        }

        IntPtr module = GetModuleHandleW(null);
        _mouseHook = SetWindowsHookExW(WH_MOUSE_LL, _mouseProc, module, 0);
        _keyHook = SetWindowsHookExW(WH_KEYBOARD_LL, _keyProc, module, 0);
        if (_mouseHook == IntPtr.Zero || _keyHook == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            Dispose();
            throw new InvalidOperationException("입력 훅 설치 실패: " + err);
        }
    }

    private unsafe IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var s = (MSLLHOOKSTRUCT*)lParam;
                if ((s->Flags & LLMHF_INJECTED) == 0 && Handle((int)wParam, s->MouseData)) return 1;
            }
            catch { Fail(); }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private bool Handle(int msg, uint data)
    {
        switch (msg)
        {
            case WM_MOUSEMOVE: return _controller.OnHookMouseMove();
            case WM_LBUTTONDOWN: return _controller.OnMouseButton(PhoneMouseButtons.Left, true);
            case WM_LBUTTONUP: return _controller.OnMouseButton(PhoneMouseButtons.Left, false);
            case WM_RBUTTONDOWN: return _controller.OnMouseButton(PhoneMouseButtons.Right, true);
            case WM_RBUTTONUP: return _controller.OnMouseButton(PhoneMouseButtons.Right, false);
            case WM_MBUTTONDOWN: return _controller.OnMouseButton(PhoneMouseButtons.Middle, true);
            case WM_MBUTTONUP: return _controller.OnMouseButton(PhoneMouseButtons.Middle, false);
            case WM_MOUSEWHEEL:
                _wheelRemainder += (short)(data >> 16);
                int notches = _wheelRemainder / 120;
                _wheelRemainder -= notches * 120;
                return _controller.OnWheel(notches);
            case WM_XBUTTONDOWN:
            case WM_XBUTTONUP:
                // mouseData 상위 워드: 1 = XBUTTON1(뒤로), 2 = XBUTTON2(앞으로)
                var x = (data >> 16) switch { 1 => PhoneMouseButtons.Back, 2 => PhoneMouseButtons.Forward, _ => PhoneMouseButtons.None };
                if (x == PhoneMouseButtons.None) return _controller.Mode == InputMode.Phone;
                return _controller.OnMouseButton(x, msg == WM_XBUTTONDOWN);
            case WM_MOUSEHWHEEL:
                return _controller.Mode == InputMode.Phone;   // 지원하지 않는 입력은 폰 모드에서 삼키기만
            default: return false;
        }
    }

    private unsafe IntPtr KeyProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var k = (KBDLLHOOKSTRUCT*)lParam;
                if ((k->Flags & LLKHF_INJECTED) == 0
                    && _controller.OnKey((int)k->VkCode, (int)k->ScanCode, (k->Flags & LLKHF_EXTENDED) != 0, (k->Flags & LLKHF_UP) == 0))
                    return 1;
            }
            catch { Fail(); }
        }
        return CallNextHookEx(_keyHook, nCode, wParam, lParam);
    }

    private void OnRawInput(IntPtr hRaw)
    {
        try
        {
            uint size = (uint)Marshal.SizeOf<RAWINPUTMOUSE>();
            if (GetRawInputData(hRaw, RID_INPUT, out var raw, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>()) == uint.MaxValue) return;
            if (raw.Header.Type != 0 || raw.Header.Device == IntPtr.Zero) return;   // 마우스가 아니거나 주입된 입력
            if ((raw.Mouse.Flags & MOUSE_MOVE_ABSOLUTE) != 0) return;
            if (raw.Mouse.LastX == 0 && raw.Mouse.LastY == 0) return;
            GetCursorPos(out var p);
            _controller.OnRawMove(raw.Mouse.LastX, raw.Mouse.LastY, p.X, p.Y, _rightEdgeMonitor(p.X, p.Y));
        }
        catch { Fail(); }
    }

    private void Fail()
    {
        try { _controller.EmergencyExit(); } catch { /* 훅 안에서는 더 할 수 있는 게 없다 */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        if (_keyHook != IntPtr.Zero) UnhookWindowsHookEx(_keyHook);
        _mouseHook = _keyHook = IntPtr.Zero;
        _window.DestroyHandle();
    }

    private sealed class RawWindow : NativeWindow
    {
        private readonly Action<IntPtr> _onInput;
        public RawWindow(Action<IntPtr> onInput)
        {
            _onInput = onInput;
            CreateHandle(new CreateParams { Parent = new IntPtr(-3) }); // HWND_MESSAGE
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_INPUT) _onInput(m.LParam);
            base.WndProc(ref m);
        }
    }
}
