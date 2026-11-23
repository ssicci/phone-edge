using PhoneEdge.App.Core;
using PhoneEdge.Contracts;
using Xunit;

namespace PhoneEdge.App.Tests;

internal sealed class StubLink : IPhoneLink
{
    public PhoneLinkState State { get; set; } = PhoneLinkState.Ready;
    public event Action<PhoneLinkState>? StateChanged { add { } remove { } }
    public event Action<double>? ExitRequested { add { } remove { } }
    public List<string> Calls = new();
    public Task StartAsync(CancellationToken c) => Task.CompletedTask;
    public void Enter(double y) => Calls.Add($"Enter {y:F2}");
    public void Leave() => Calls.Add("Leave");
    public void SendMouse(int dx, int dy, PhoneMouseButtons b, int w) => Calls.Add($"Mouse {dx},{dy},{b},{w}");
    public void SendKeyboard(byte m, ReadOnlySpan<byte> k) => Calls.Add($"Key {m:X2},{string.Join(' ', k.ToArray().Select(x => x.ToString("X2")))}");
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class StubCursor : ICursorPlacer
{
    public List<double> Placed = new();
    public void PlaceAtRightEdge(ScreenRect m, double y) => Placed.Add(y);
}

public class ModeControllerTests
{
    private static readonly ScreenRect Mon = new(0, 0, 1920, 1081);   // 높이 1081 → y 540 = 0.5
    private readonly StubLink _link = new();
    private readonly StubCursor _cursor = new();
    private readonly ModeController _c;

    public ModeControllerTests() => _c = new ModeController(_link, _cursor);

    private void PushRight(int y = 540) => _c.OnRawMove(5, 0, 1919, y, Mon);

    [Fact]
    public void EntersPhoneModeWhenPushingPastRightEdge()
    {
        PushRight();
        Assert.Equal(InputMode.Phone, _c.Mode);
        Assert.Equal("Enter 0.50", _link.Calls[0]);
    }

    [Fact]
    public void DoesNotEnterWhenNotAtEdgeMovingLeftNotReadyOrNotRightmostMonitor()
    {
        _c.OnRawMove(5, 0, 1000, 540, Mon);
        _c.OnRawMove(-5, 0, 1919, 540, Mon);
        _c.OnRawMove(5, 0, 1919, 540, null);
        _link.State = PhoneLinkState.Connecting;
        PushRight();
        Assert.Equal(InputMode.Laptop, _c.Mode);
        Assert.Empty(_link.Calls);
    }

    [Fact]
    public void DoesNotEnterWhileMouseButtonHeldOrPaused()
    {
        _c.OnMouseButton(PhoneMouseButtons.Left, true);
        PushRight();
        Assert.Equal(InputMode.Laptop, _c.Mode);
        _c.OnMouseButton(PhoneMouseButtons.Left, false);
        _c.Enabled = false;
        PushRight();
        Assert.Equal(InputMode.Laptop, _c.Mode);
    }

    [Fact]
    public void PhoneModeForwardsMouseAndSwallowsHookInput()
    {
        PushRight();
        _c.OnRawMove(3, -2, 1919, 540, Mon);
        Assert.True(_c.OnHookMouseMove());
        Assert.True(_c.OnMouseButton(PhoneMouseButtons.Left, true));
        Assert.True(_c.OnWheel(2));
        Assert.Contains("Mouse 3,-2,None,0", _link.Calls);
        Assert.Contains("Mouse 0,0,Left,0", _link.Calls);
        Assert.Contains("Mouse 0,0,Left,2", _link.Calls);
    }

    [Fact]
    public void ExitRequestedReturnsToLaptopAndPlacesCursor()
    {
        PushRight();
        _c.OnExitRequested(0.25);
        Assert.Equal(InputMode.Laptop, _c.Mode);
        Assert.Equal("Leave", _link.Calls[^1]);
        Assert.Equal(0.25, _cursor.Placed[0]);
        Assert.False(_c.OnHookMouseMove());
    }

    [Fact]
    public void CtrlAltEscEmergencyExit()
    {
        PushRight();
        Assert.True(_c.OnKey(0xA2, 0x1D, false, true));   // LCtrl
        Assert.True(_c.OnKey(0xA4, 0x38, false, true));   // LAlt
        Assert.True(_c.OnKey(0x1B, 0x01, false, true));   // Esc
        Assert.Equal(InputMode.Laptop, _c.Mode);
        Assert.Equal("Leave", _link.Calls[^1]);
        // 폰 모드에서 눌린 키의 뗌은 Windows 에 보내지 않는다
        Assert.True(_c.OnKey(0x1B, 0x01, false, false));
        Assert.True(_c.OnKey(0xA4, 0x38, false, false));
        Assert.True(_c.OnKey(0xA2, 0x1D, false, false));
        Assert.False(_c.OnKey(0x41, 0x1E, false, true));  // 이후 일반 입력은 통과
    }

    [Fact]
    public void LinkLeavingReadyForcesLaptopMode()
    {
        PushRight();
        _c.OnLinkStateChanged(PhoneLinkState.Disconnected);
        Assert.Equal(InputMode.Laptop, _c.Mode);
        Assert.Equal("Leave", _link.Calls[^1]);
    }

    [Fact]
    public void PausingInPhoneModeLeaves()
    {
        PushRight();
        _c.Enabled = false;
        Assert.Equal(InputMode.Laptop, _c.Mode);
    }

    [Fact]
    public void KeyboardSendsFullStateAndPassesWindowsHeldKeyRelease()
    {
        Assert.False(_c.OnKey(0xA0, 0x2A, false, true));   // 노트북 모드에서 Shift 누름 (Windows 가 안다)
        PushRight();
        Assert.True(_c.OnKey(0x41, 0x1E, false, true));    // A
        Assert.Equal("Key 00,04", _link.Calls[^1]);
        Assert.True(_c.OnKey(0x41, 0x1E, false, true));    // 자동 반복은 다시 보내지 않음
        Assert.Single(_link.Calls, c => c.StartsWith("Key"));
        Assert.False(_c.OnKey(0xA0, 0x2A, false, false));  // Shift 뗌은 Windows 로
        Assert.True(_c.OnKey(0x41, 0x1E, false, false));
        Assert.Equal("Key 00,", _link.Calls[^1]);
    }

    [Fact]
    public void BackButtonInPhoneModeGoesToPhone()
    {
        PushRight();
        Assert.True(_c.OnMouseButton(PhoneMouseButtons.Back, true));
        Assert.True(_c.OnMouseButton(PhoneMouseButtons.Back, false));
        Assert.Contains("Mouse 0,0,Back,0", _link.Calls);
        Assert.Contains("Mouse 0,0,None,0", _link.Calls);
    }

    [Fact]
    public void ButtonHeldInPhoneModeIsReleasedSilentlyOnReturn()
    {
        PushRight();
        _c.OnMouseButton(PhoneMouseButtons.Left, true);
        _c.OnExitRequested(0.5);
        Assert.True(_c.OnMouseButton(PhoneMouseButtons.Left, false));
    }
}
