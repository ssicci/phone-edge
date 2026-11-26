using PhoneEdge.App.Core;
using PhoneEdge.Contracts;
using Xunit;

namespace PhoneEdge.App.Tests;

/// <summary>4방향 진입 판정과 비율, 복귀 위치, 오진입 방지(모서리 제외·밀어내기 누적), 모니터 변경 시 폰 모드 해제.</summary>
public class EntryGuardTests
{
    // 모니터: x 0~2000, y 0~1000 (계산하기 쉬운 값)
    private static readonly ScreenRect Mon = new(0, 0, 2000, 1000);

    private readonly StubLink _link = new();
    private readonly StubCursor _cursor = new();
    private long _now;

    private ModeController Make(PhoneSide side, EntryGuard guard, int start, int end)
        => new(_link, _cursor, guard, () => _now) { Slot = new ResolvedSlot(Mon, side, start, end) };

    // ---- 4방향 진입과 ratio ----

    [Theory]
    [InlineData(PhoneSide.Right, 1999, 250, 5, 0, PhoneEntryEdge.Left, "Enter 0.25")]    // 오른쪽 변(y 0~1000): 250/999
    [InlineData(PhoneSide.Left, 0, 750, -5, 0, PhoneEntryEdge.Right, "Enter 0.75")]
    [InlineData(PhoneSide.Top, 500, 0, 0, -5, PhoneEntryEdge.Bottom, "Enter 0.25")]      // 위 변(x 0~2000): 500/1999
    [InlineData(PhoneSide.Bottom, 1500, 999, 0, 5, PhoneEntryEdge.Top, "Enter 0.75")]
    public void EntersThroughTheFacingEdgeWithTheRelativePosition(PhoneSide side, int x, int y, int dx, int dy, PhoneEntryEdge edge, string call)
    {
        var c = Make(side, EntryGuard.None, 0, side is PhoneSide.Right or PhoneSide.Left ? 1000 : 2000);
        c.OnRawMove(dx, dy, x, y);
        Assert.Equal(InputMode.Phone, c.Mode);
        Assert.Equal(edge, _link.Edges[0]);
        Assert.StartsWith(call[..8], _link.Calls[0]);   // 비율은 소수 둘째 자리까지만 비교
    }

    [Fact]
    public void RatioIsRelativeToTheSlotSpanNotTheMonitor()
    {
        // 오른쪽 변에서 폰 자리가 y 400~800 이다 (400 높이): 커서 y=600 → (600-400)/(399)
        var c = Make(PhoneSide.Right, EntryGuard.None, 400, 800);
        c.OnRawMove(5, 0, 1999, 600);
        Assert.Equal("Enter 0.50", _link.Calls[0]);
        // 구간 밖(y=300)에서는 진입하지 않는다
        var link2 = new StubLink();
        var c2 = new ModeController(link2, _cursor, EntryGuard.None) { Slot = new ResolvedSlot(Mon, PhoneSide.Right, 400, 800) };
        c2.OnRawMove(5, 0, 1999, 300);
        Assert.Equal(InputMode.Laptop, c2.Mode);
    }

    [Theory]
    [InlineData(PhoneSide.Right, -5, 0, 1999, 500)]     // 반대 방향으로 밀기
    [InlineData(PhoneSide.Right, 5, 0, 1000, 500)]      // 경계가 아님
    [InlineData(PhoneSide.Left, 5, 0, 0, 500)]
    [InlineData(PhoneSide.Top, 0, 5, 1000, 0)]
    [InlineData(PhoneSide.Bottom, 0, -5, 1000, 999)]
    [InlineData(PhoneSide.Right, 0, 5, 1999, 500)]      // 경계를 따라 미끄러지기만 함
    public void IgnoresMovesThatAreNotOutwardAtTheEdge(PhoneSide side, int dx, int dy, int x, int y)
    {
        var c = Make(side, EntryGuard.None, 0, side is PhoneSide.Right or PhoneSide.Left ? 1000 : 2000);
        c.OnRawMove(dx, dy, x, y);
        Assert.Equal(InputMode.Laptop, c.Mode);
    }

    [Fact]
    public void OtherEdgesOfTheSameMonitorDoNotEnter()
    {
        var c = Make(PhoneSide.Right, EntryGuard.None, 0, 1000);
        c.OnRawMove(-5, 0, 0, 500);    // 왼쪽 끝
        c.OnRawMove(0, -5, 1000, 0);   // 위쪽 끝
        c.OnRawMove(0, 5, 1000, 999);  // 아래쪽 끝
        Assert.Equal(InputMode.Laptop, c.Mode);
    }

    // ---- 복귀 위치 ----

    [Theory]
    [InlineData(PhoneSide.Right, 2000 - 1 - ModeController.ReturnInset, 500)]
    [InlineData(PhoneSide.Left, ModeController.ReturnInset, 500)]
    public void ReturnPlacesTheCursorInsideTheEdgeAtTheSameRelativeHeight(PhoneSide side, int x, int y)
    {
        var c = Make(side, EntryGuard.None, 0, 1000);
        c.OnRawMove(side == PhoneSide.Right ? 5 : -5, 0, side == PhoneSide.Right ? 1999 : 0, 500);
        c.OnExitRequested(500.0 / 999);
        Assert.Equal(InputMode.Laptop, c.Mode);
        Assert.Equal((x, y), _cursor.Placed[^1]);
    }

    [Theory]
    [InlineData(PhoneSide.Top, 1000, ModeController.ReturnInset)]
    [InlineData(PhoneSide.Bottom, 1000, 1000 - 1 - ModeController.ReturnInset)]
    public void ReturnPlacesTheCursorInsideTopAndBottomEdges(PhoneSide side, int x, int y)
    {
        var c = Make(side, EntryGuard.None, 0, 2000);
        c.OnRawMove(0, side == PhoneSide.Top ? -5 : 5, 1000, side == PhoneSide.Top ? 0 : 999);
        Assert.Equal(InputMode.Phone, c.Mode);
        c.OnExitRequested(1000.0 / 1999);
        Assert.Equal((x, y), _cursor.Placed[^1]);
    }

    [Fact]
    public void ReturnUsesTheSlotThatWasEnteredEvenIfTheLayoutChangedMeanwhile()
    {
        var c = Make(PhoneSide.Right, EntryGuard.None, 0, 1000);
        c.OnRawMove(5, 0, 1999, 500);
        c.Slot = new ResolvedSlot(Mon, PhoneSide.Left, 0, 1000);   // 폰 모드 중에 바뀜 → 먼저 복귀
        Assert.Equal(InputMode.Laptop, c.Mode);
        Assert.Equal("Leave", _link.Calls[^1]);
        Assert.Equal(2000 - 1 - ModeController.ReturnInset, _cursor.Placed[^1].X);   // 들어갈 때의 폰 자리(오른쪽)에 놓는다
    }

    [Fact]
    public void SettingTheSameSlotDoesNotLeavePhoneMode()
    {
        var c = Make(PhoneSide.Right, EntryGuard.None, 0, 1000);
        c.OnRawMove(5, 0, 1999, 500);
        c.Slot = new ResolvedSlot(Mon, PhoneSide.Right, 0, 1000);
        Assert.Equal(InputMode.Phone, c.Mode);
    }

    // ---- 오진입 방지: 모서리 ----

    [Theory]
    [InlineData(0)]     // 맨 위 모서리 (닫기 단추 쪽)
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(900)]
    [InlineData(999)]   // 맨 아래 모서리 (시작 단추·작업 표시줄 쪽)
    public void CornersOfTheSpanAreExcluded(int y)
    {
        var c = Make(PhoneSide.Right, new EntryGuard(0.10, 0, 0), 0, 1000);   // 각 끝 10% = 100px
        c.OnRawMove(50, 0, 1999, y);
        Assert.Equal(InputMode.Laptop, c.Mode);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(899)]
    public void TheMiddleOfTheSpanEnters(int y)
    {
        var c = Make(PhoneSide.Right, new EntryGuard(0.10, 0, 0), 0, 1000);
        c.OnRawMove(50, 0, 1999, y);
        Assert.Equal(InputMode.Phone, c.Mode);
    }

    [Fact]
    public void CornerRatioIsRelativeToTheSpanNotTheMonitor()
    {
        // 폰 자리가 y 400~600 (200px): 각 끝 10% = 20px → 400~419 와 581~599 는 모서리
        var c = Make(PhoneSide.Right, new EntryGuard(0.10, 0, 0), 400, 600);
        c.OnRawMove(5, 0, 1999, 410);
        Assert.Equal(InputMode.Laptop, c.Mode);
        c.OnRawMove(5, 0, 1999, 590);
        Assert.Equal(InputMode.Laptop, c.Mode);
        c.OnRawMove(5, 0, 1999, 500);
        Assert.Equal(InputMode.Phone, c.Mode);
    }

    // ---- 오진입 방지: 밀어내기 누적 ----

    [Fact]
    public void MustPushAFurtherDistanceBeyondTheEdge()
    {
        var c = Make(PhoneSide.Right, new EntryGuard(0, 120, 500), 0, 1000);
        c.OnRawMove(50, 0, 1999, 500);
        c.OnRawMove(50, 0, 1999, 500);
        Assert.Equal(InputMode.Laptop, c.Mode);   // 100 < 120
        c.OnRawMove(30, 0, 1999, 500);
        Assert.Equal(InputMode.Phone, c.Mode);    // 130 ≥ 120
    }

    [Fact]
    public void PushWorksTheSameInAllFourDirections()
    {
        foreach (var (side, dx, dy, x, y) in new[]
        {
            (PhoneSide.Left, -60, 0, 0, 500), (PhoneSide.Top, 0, -60, 1000, 0), (PhoneSide.Bottom, 0, 60, 1000, 999),
        })
        {
            var link = new StubLink();
            var c = new ModeController(link, _cursor, new EntryGuard(0, 100, 500), () => _now) { Slot = new ResolvedSlot(Mon, side, 0, side is PhoneSide.Left ? 1000 : 2000) };
            c.OnRawMove(dx, dy, x, y);
            Assert.Equal(InputMode.Laptop, c.Mode);
            c.OnRawMove(dx, dy, x, y);
            Assert.Equal(InputMode.Phone, c.Mode);
        }
    }

    [Fact]
    public void MovingTheOtherWayResetsTheAccumulation()
    {
        var c = Make(PhoneSide.Right, new EntryGuard(0, 120, 500), 0, 1000);
        c.OnRawMove(100, 0, 1999, 500);
        c.OnRawMove(-3, 0, 1999, 500);            // 반대로 조금 움직이면 처음부터
        c.OnRawMove(100, 0, 1999, 500);
        Assert.Equal(InputMode.Laptop, c.Mode);
        c.OnRawMove(30, 0, 1999, 500);
        Assert.Equal(InputMode.Phone, c.Mode);
    }

    [Fact]
    public void LeavingTheEdgeResetsTheAccumulation()
    {
        var c = Make(PhoneSide.Right, new EntryGuard(0, 120, 500), 0, 1000);
        c.OnRawMove(100, 0, 1999, 500);
        c.OnRawMove(-5, 0, 1990, 500);    // 경계에서 떨어짐
        c.OnRawMove(30, 0, 1999, 500);
        Assert.Equal(InputMode.Laptop, c.Mode);   // 30 만 쌓임
    }

    [Fact]
    public void SlidingAlongTheEdgeKeepsTheAccumulation()
    {
        var c = Make(PhoneSide.Right, new EntryGuard(0, 120, 500), 0, 1000);
        c.OnRawMove(100, 0, 1999, 500);
        c.OnRawMove(0, 40, 1999, 500);    // 경계를 따라 미끄러짐 (바깥 방향 0)
        c.OnRawMove(30, 0, 1999, 540);
        Assert.Equal(InputMode.Phone, c.Mode);
    }

    [Fact]
    public void PausingInTheMiddleOfAPushForgetsIt()
    {
        var c = Make(PhoneSide.Right, new EntryGuard(0, 120, 500), 0, 1000);
        c.OnRawMove(100, 0, 1999, 500);
        _now += 501;                       // 멈췄다가
        c.OnRawMove(30, 0, 1999, 500);     // 다시 밀면 새 동작
        Assert.Equal(InputMode.Laptop, c.Mode);
        _now += 100;
        c.OnRawMove(100, 0, 1999, 500);    // 같은 동작 안의 짧은 멈칫은 합쳐진다
        Assert.Equal(InputMode.Phone, c.Mode);
    }

    [Fact]
    public void AccumulationDoesNotCarryOverAfterReturning()
    {
        var c = Make(PhoneSide.Right, new EntryGuard(0, 120, 500), 0, 1000);
        c.OnRawMove(130, 0, 1999, 500);
        Assert.Equal(InputMode.Phone, c.Mode);
        c.OnExitRequested(0.5);
        c.OnRawMove(50, 0, 1999, 500);    // 돌아온 직후 바로 다시 들어가지 않는다
        Assert.Equal(InputMode.Laptop, c.Mode);
    }

    [Fact]
    public void DefaultGuardIsTheDocumentedValues()
    {
        Assert.Equal(0.10, EntryGuard.Default.CornerRatio);
        Assert.Equal(120, EntryGuard.Default.PushCounts);
        Assert.Equal(500, EntryGuard.Default.IdleResetMs);
        Assert.Equal(new ModeController(_link, _cursor).Guard, EntryGuard.Default);
    }

    // ---- 모니터 변경 ----

    [Fact]
    public void ChangingTheSlotBecauseTheDisplayChangedLeavesPhoneMode()
    {
        var c = Make(PhoneSide.Right, EntryGuard.None, 0, 1000);
        c.OnRawMove(5, 0, 1999, 500);
        Assert.Equal(InputMode.Phone, c.Mode);
        c.Slot = new ResolvedSlot(new ScreenRect(0, 0, 1000, 500), PhoneSide.Right, 0, 500);
        Assert.Equal(InputMode.Laptop, c.Mode);
        Assert.Equal("Leave", _link.Calls[^1]);
    }

    // ---- 가짜 폰 (개발용) 의 4방향 ----

    [Theory]
    [InlineData(PhoneEntryEdge.Top, 0, -50, 0.25)]      // 위 변으로 들어와 위로 나가면 복귀, 비율은 x
    [InlineData(PhoneEntryEdge.Bottom, 0, 50, 0.25)]
    [InlineData(PhoneEntryEdge.Left, -50, 0, 0.25)]
    [InlineData(PhoneEntryEdge.Right, 50, 0, 0.25)]
    public async Task FakeLinkExitsThroughTheEntryEdge(PhoneEntryEdge edge, int dx, int dy, double ratio)
    {
        var fake = new FakePhoneLink(_ => { });
        await fake.StartAsync(default);
        double? exit = null;
        fake.ExitRequested += y => exit = y;
        fake.Enter(ratio, edge);
        // 안쪽으로 한참 들어갔다 나오면 (반대 방향) 넘지 않았으니 복귀하지 않는다
        fake.SendMouse(-dx, -dy, PhoneMouseButtons.None, 0);
        Assert.Null(exit);
        fake.SendMouse(dx * 40, dy * 40, PhoneMouseButtons.None, 0);   // 진입 경계를 바깥으로 넘음
        Assert.NotNull(exit);
        Assert.Equal(ratio, exit!.Value, 2);
    }

    [Fact]
    public void MonitorProviderGivesTheSameMonitorsAndIdsEachTime()
    {
        var a = PhoneEdge.App.Native.MonitorProvider.Current();
        var b = PhoneEdge.App.Native.MonitorProvider.Current();
        Assert.NotEmpty(a);
        Assert.Equal(a.Select(m => m.Id), b.Select(m => m.Id));
        Assert.Equal(a.Count, a.Select(m => m.Id).Distinct().Count());   // 모니터마다 다른 ID
    }
}
