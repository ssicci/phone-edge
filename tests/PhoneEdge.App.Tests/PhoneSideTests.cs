using PhoneEdge.App.Core;
using PhoneEdge.Contracts;
using Xunit;

namespace PhoneEdge.App.Tests;

public class PhoneSideTests
{
    private static readonly ScreenRect Mon = new(0, 0, 1920, 1081);   // y 540 = 0.5
    private readonly StubLink _link = new();
    private readonly StubCursor _cursor = new();
    private readonly ModeController _c;

    public PhoneSideTests() => _c = new ModeController(_link, _cursor) { Side = PhoneSide.Left };

    private void PushLeft(int y = 540) => _c.OnRawMove(-5, 0, 0, y, Mon);

    [Fact]
    public void LeftSideEntersFromLeftEdgeThroughPhoneRightEdge()
    {
        PushLeft();
        Assert.Equal(InputMode.Phone, _c.Mode);
        Assert.Equal("Enter 0.50", _link.Calls[0]);
        Assert.Equal(PhoneEntryEdge.Right, _link.Edges[0]);
    }

    [Fact]
    public void LeftSideIgnoresPushingRightAndPushingAwayFromTheEdge()
    {
        _c.OnRawMove(5, 0, 0, 540, Mon);        // 오른쪽으로 미는 것은 무시
        _c.OnRawMove(-5, 0, 800, 540, Mon);     // 끝이 아님
        _c.OnRawMove(-5, 0, 1919, 540, Mon);    // 오른쪽 끝에서 왼쪽으로: 끝이 아님
        _c.OnRawMove(-5, 0, 0, 540, null);      // 가장 왼쪽 모니터가 아님
        Assert.Equal(InputMode.Laptop, _c.Mode);
        Assert.Empty(_link.Calls);
    }

    [Fact]
    public void RightSideIgnoresPushingLeft()
    {
        _c.Side = PhoneSide.Right;
        PushLeft();
        Assert.Equal(InputMode.Laptop, _c.Mode);
        _c.OnRawMove(5, 0, 1919, 540, Mon);
        Assert.Equal(PhoneEntryEdge.Left, _link.Edges[0]);
    }

    [Fact]
    public void ExitPlacesCursorAtLeftEdgeSameHeight()
    {
        PushLeft(270);
        _c.OnExitRequested(0.25);
        Assert.Equal(InputMode.Laptop, _c.Mode);
        Assert.Equal(new[] { 0.25 }, _cursor.PlacedLeft);
        Assert.Empty(_cursor.Placed);
    }

    [Fact]
    public void EmergencyExitPlacesCursorAtLeftEdge()
    {
        PushLeft();
        _c.EmergencyExit();
        Assert.Single(_cursor.PlacedLeft);
        Assert.Equal(0.5, _cursor.PlacedLeft[0], 2);
    }

    [Fact]
    public void ChangingSideInPhoneModeReturnsToLaptopFirstAtTheEntrySide()
    {
        PushLeft();
        _c.Side = PhoneSide.Right;
        Assert.Equal(InputMode.Laptop, _c.Mode);
        Assert.Equal("Leave", _link.Calls[^1]);
        Assert.Single(_cursor.PlacedLeft);   // 들어갈 때의 쪽(왼쪽)에 놓는다
        Assert.Empty(_cursor.Placed);
        _c.OnRawMove(5, 0, 1919, 540, Mon);  // 새 설정이 바로 적용된다
        Assert.Equal(InputMode.Phone, _c.Mode);
    }

    [Fact]
    public void SettingTheSameSideInPhoneModeDoesNothing()
    {
        PushLeft();
        _c.Side = PhoneSide.Left;
        Assert.Equal(InputMode.Phone, _c.Mode);
    }

    [Fact]
    public void SideSettingIsSavedAndRead()
    {
        string path = Path.Combine(Path.GetTempPath(), "PhoneEdgeTests-" + Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            var store = new SettingsStore(path);
            Assert.Equal(PhoneSide.Right, store.PhoneSide);   // 기본
            store.PhoneSide = PhoneSide.Left;
            Assert.Equal(PhoneSide.Left, new SettingsStore(path).PhoneSide);
            Assert.Contains("\"left\"", File.ReadAllText(path));
            store.PhoneSide = PhoneSide.Right;
            Assert.Equal(PhoneSide.Right, new SettingsStore(path).PhoneSide);
        }
        finally { try { Directory.Delete(Path.GetDirectoryName(path)!, true); } catch { } }
    }

    [Theory]
    [InlineData(null, PhoneSide.Right)]
    [InlineData("right", PhoneSide.Right)]
    [InlineData("left", PhoneSide.Left)]
    [InlineData("up", PhoneSide.Right)]
    public void ParseFallsBackToRight(string? text, PhoneSide expected) => Assert.Equal(expected, PhoneSides.Parse(text));

    [Fact]
    public async Task FakeLinkSimulatesRightEntryAndExit()
    {
        var fake = new FakePhoneLink(_ => { });
        await fake.StartAsync(default);
        double? exit = null;
        fake.ExitRequested += y => exit = y;
        fake.Enter(0.5, PhoneEntryEdge.Right);
        fake.SendMouse(-100, 0, PhoneMouseButtons.None, 0);   // 폰 안쪽으로
        Assert.Null(exit);
        fake.SendMouse(200, 0, PhoneMouseButtons.None, 0);    // 오른쪽 끝을 넘음
        Assert.NotNull(exit);
    }
}
