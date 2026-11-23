using PhoneEdge.Contracts;
using PhoneEdge.Phone.Adb;
using PhoneEdge.Phone.Protocol;
using PhoneEdge.Phone.Tracking;

namespace PhoneEdge.Phone.Tests;

public class TrackingTests
{
    private static CursorTracker NewTracker(double gain = 1.0)
    {
        var t = new CursorTracker { Gain = gain, ExitThreshold = 20 };
        t.SetScreen(1000, 2001);
        return t;
    }

    [Fact]
    public void Enter_homes_to_corner_then_moves_down_to_ratio()
    {
        var t = NewTracker(gain: 2.0);
        var moves = t.Enter(0.5);
        Assert.Equal(2, moves.Count);
        Assert.True(moves[0].Dx * 2.0 <= -2001 && moves[0].Dy * 2.0 <= -2001);
        Assert.Equal((0L, 500L), moves[1]); // 1000 px / gain 2
        Assert.Equal(0, t.X);
        Assert.Equal(1000, t.Y);
    }

    [Fact]
    public void Small_left_push_does_not_exit_but_threshold_does()
    {
        var t = NewTracker();
        t.Enter(0.25);
        Assert.False(t.Move(-10, 0, false, out _));
        Assert.True(t.Move(-10, 0, false, out double y));
        Assert.Equal(0.25, y, 3);
        Assert.False(t.Active);
    }

    [Fact]
    public void Clamp_on_right_wall_resets_error()
    {
        var t = NewTracker();
        t.Enter(0);
        t.Move(5000, 0, false, out _); // 오른쪽 벽에 붙음
        Assert.Equal(999, t.X);
        Assert.False(t.Move(-999, 0, false, out _)); // 정확히 좌측 벽
        Assert.Equal(0, t.X);
        Assert.True(t.Move(-25, 0, false, out _));
    }

    [Fact]
    public void Moving_right_resets_accumulated_push()
    {
        var t = NewTracker();
        t.Enter(0);
        t.Move(-15, 0, false, out _);
        t.Move(1, 0, false, out _);
        Assert.False(t.Move(-15, 0, false, out _)); // 1 px 앞으로 갔다가 14 px 초과 → 누적 14 < 20
    }

    [Fact]
    public void No_exit_while_button_held()
    {
        var t = NewTracker();
        t.Enter(0);
        Assert.False(t.Move(-500, 0, true, out _));
        Assert.True(t.Move(-1, 0, false, out _)); // 버튼을 떼고 조금만 밀어도 (누적 이미 넘음) 이탈
    }

    [Fact]
    public void Y_is_clamped_and_reported_as_ratio()
    {
        var t = NewTracker();
        t.Enter(0.5);
        t.Move(0, 99999, false, out _);
        Assert.True(t.Move(-100, 0, false, out double y));
        Assert.Equal(1.0, y, 6);
    }

    [Fact]
    public void Gain_scales_motion()
    {
        var t = NewTracker(gain: 1.5);
        t.Enter(0);
        t.Move(100, 0, false, out _);
        Assert.Equal(150, t.X);
    }

    [Fact]
    public void SetScreen_reports_change_only_when_different()
    {
        var t = NewTracker();
        Assert.False(t.SetScreen(1000, 2001));
        Assert.True(t.SetScreen(2001, 1000));
    }

    // ---- PhoneInputProcessor (소켓 없이) ----

    private sealed class Sink
    {
        public readonly List<byte[]> Sent = [];
        public readonly List<double> Exits = [];
        public PhoneInputProcessor P { get; }
        public Sink() { P = new PhoneInputProcessor(Sent.Add, Exits.Add); P.Tracker.ExitThreshold = 20; P.Resize(1000, 2000); }
    }

    [Fact]
    public void Processor_ignores_input_outside_phone_mode()
    {
        var s = new Sink();
        s.P.Mouse(5, 5, PhoneMouseButtons.None, 0);
        s.P.Keyboard(0, [4]);
        Assert.Empty(s.Sent);
    }

    [Fact]
    public void Processor_exit_releases_buttons_and_keys_and_raises_once()
    {
        var s = new Sink();
        s.P.Enter(0.5);
        s.P.Keyboard(0x02, [0x04]);
        s.P.Mouse(10, 0, PhoneMouseButtons.Left, 0);
        s.P.Mouse(0, 0, PhoneMouseButtons.None, 0); // 버튼 뗌
        s.P.Keyboard(0x02, [0x04]);                 // 키는 눌린 채
        s.Sent.Clear();
        s.P.Mouse(-100, 0, PhoneMouseButtons.None, 0);
        Assert.Equal([0.5], s.Exits.Select(e => Math.Round(e, 3)));
        Assert.False(s.P.InPhoneMode);
        // 키보드 해제 리포트만 (마우스 버튼은 이미 뗀 상태)
        var msg = Assert.Single(s.Sent);
        Assert.Equal(ControlMessages.UhidInput(HidReports.KeyboardId, new byte[8]), msg);
        s.P.Mouse(-100, 0, PhoneMouseButtons.None, 0);
        Assert.Single(s.Exits);
    }

    [Fact]
    public void Processor_rehomes_on_rotation_during_phone_mode()
    {
        var s = new Sink();
        s.P.Enter(0.5);
        s.Sent.Clear();
        s.P.Resize(2000, 1000);
        Assert.NotEmpty(s.Sent);
        Assert.All(s.Sent, m => Assert.Equal(ControlMessages.TypeUhidInput, m[0]));
        Assert.Equal(0, s.P.Tracker.X);
    }

    [Fact]
    public void Processor_creates_keyboard_and_mouse()
    {
        var s = new Sink();
        s.P.CreateDevices();
        Assert.Equal(2, s.Sent.Count);
        Assert.All(s.Sent, m => Assert.Equal(ControlMessages.TypeUhidCreate, m[0]));
    }

    // ---- adb 출력 파서 ----

    [Fact]
    public void ParseDevices_reads_states()
    {
        var d = AdbParsers.ParseDevices("* daemon started successfully\r\nList of devices attached\r\nR5CX123\tdevice\r\nABC\tunauthorized\r\n\r\n");
        Assert.Equal([new AdbDevice("R5CX123", "device"), new AdbDevice("ABC", "unauthorized")], d);
    }

    [Theory]
    [InlineData("    init=1080x2520 420dpi base=1080x2520 420dpi cur=2520x1080 app=2520x1004 rng=1080x1004-2520x2400", 2520, 1080)]
    [InlineData("  mOverrideDisplayInfo=DisplayInfo{\"Built-in Screen\", displayId 0, real 1856 x 2160, largest app 2160 x 2160", 1856, 2160)]
    public void TryParseDisplaySize_formats(string text, int w, int h)
    {
        Assert.True(AdbParsers.TryParseDisplaySize(text, out int pw, out int ph));
        Assert.Equal((w, h), (pw, ph));
    }

    [Fact]
    public void TryParseDisplaySize_fails_on_garbage() => Assert.False(AdbParsers.TryParseDisplaySize("nothing", out _, out _));

    [Fact]
    public void TryParseMouseViewport_reads_display_and_frame()
    {
        const string line = "            viewport: Viewport INTERNAL: displayId=0, uniqueId=local:4630946449689556883, port=147, orientation=0, densityDpi=420 xDpi=367.552948, yDpi=367.374817, logicalFrame=[0, 0, 1968, 2184], physicalFrame=[0, 0, 1968, 2184], deviceSize=[1968, 2184], isActive=[1]";
        Assert.True(AdbParsers.TryParseMouseViewport(line, out int id, out int w, out int h));
        Assert.Equal((0, 1968, 2184), (id, w, h));
    }

    [Fact]
    public void TryParseMousePosition_reads_hover()
    {
        const string line = "DeviceId(19):[touchingPointers=[], downTimeInTarget=<not set>, hoveringPointers=[Pointer(id=0, MOUSE) at (267.969, 2183)], pilferingPointerIds=<none>]";
        Assert.True(AdbParsers.TryParseMousePosition(line, out double x, out double y));
        Assert.Equal(267.969, x, 3);
        Assert.Equal(2183, y);
    }

    [Theory]
    [InlineData("  Mouse pointer acceleration enabled: true", true)]
    [InlineData("  Mouse pointer acceleration enabled: false", false)]
    [InlineData("nothing", null)]
    public void ParseMouseAccelerationEnabled(string text, bool? expected)
        => Assert.Equal(expected, AdbParsers.ParseMouseAccelerationEnabled(text));

    [Fact]
    public void Processor_pauses_between_home_and_vertical_move_when_configured()
    {
        var log = new List<string>();
        var p = new PhoneInputProcessor(_ => log.Add("send"), _ => { }, ms => log.Add("pause" + ms)) { HomeSettleMs = 60 };
        p.Resize(1000, 2000);
        p.Enter(0.5);
        int pause = log.IndexOf("pause60");
        Assert.True(pause > 0 && pause < log.Count - 1); // 구석 이동 뒤, 세로 이동 앞
    }
}
