using Twinput.App.Core;
using Twinput.Contracts;
using Xunit;

namespace Twinput.App.Tests;

public class GuidanceTests
{
    public static IEnumerable<object[]> Problems =>
        Enum.GetValues<PhoneLinkProblem>().Where(p => p != PhoneLinkProblem.None).Select(p => new object[] { p });

    [Theory, MemberData(nameof(Problems))]
    public void EveryProblemHasCompleteGuidance(PhoneLinkProblem p)
    {
        var g = GuidanceTable.For(p);
        Assert.NotNull(g);
        Assert.False(string.IsNullOrWhiteSpace(g!.Cause));
        Assert.NotEmpty(g.Steps);
        Assert.All(g.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s)));
        Assert.InRange(TrayStatus.Tooltip(TrayIconKind.Problem, new(p, PhoneLinkWarnings.None, null)).Length, 1, 127);
    }

    [Fact]
    public void NoProblemHasNoGuidance() => Assert.Null(GuidanceTable.For(PhoneLinkProblem.None));

    [Fact]
    public void TooltipShowsShortCause()
        => Assert.Equal("Twinput - USB 디버깅 허용 필요",
            TrayStatus.Tooltip(TrayIconKind.Problem, new(PhoneLinkProblem.Unauthorized, PhoneLinkWarnings.None, null), System.Globalization.CultureInfo.GetCultureInfo("ko")));

    [Theory]
    [InlineData(false, InputMode.Laptop, PhoneLinkState.Ready, PhoneLinkProblem.None, TrayIconKind.Ready)]
    [InlineData(false, InputMode.Phone, PhoneLinkState.Ready, PhoneLinkProblem.None, TrayIconKind.Phone)]
    [InlineData(true, InputMode.Laptop, PhoneLinkState.Ready, PhoneLinkProblem.None, TrayIconKind.Paused)]
    [InlineData(true, InputMode.Laptop, PhoneLinkState.Disconnected, PhoneLinkProblem.NoDevice, TrayIconKind.Paused)]
    [InlineData(false, InputMode.Laptop, PhoneLinkState.Disconnected, PhoneLinkProblem.NoDevice, TrayIconKind.Problem)]
    [InlineData(false, InputMode.Laptop, PhoneLinkState.Disconnected, PhoneLinkProblem.None, TrayIconKind.Disconnected)]
    [InlineData(false, InputMode.Laptop, PhoneLinkState.Connecting, PhoneLinkProblem.None, TrayIconKind.Disconnected)]
    public void IconSelection(bool paused, InputMode mode, PhoneLinkState state, PhoneLinkProblem problem, TrayIconKind expected)
        => Assert.Equal(expected, TrayStatus.SelectIcon(paused, mode, state, new(problem, PhoneLinkWarnings.None, null)));

    [Fact]
    public void WarningOnlyKeepsReadyIconButEnablesGuide()
    {
        var d = new PhoneLinkDiagnosis(PhoneLinkProblem.None, PhoneLinkWarnings.PointerAcceleration, null);
        Assert.Equal(TrayIconKind.Ready, TrayStatus.SelectIcon(false, InputMode.Laptop, PhoneLinkState.Ready, d));
        Assert.True(TrayStatus.HasIssue(d));
        Assert.False(TrayStatus.HasIssue(PhoneLinkDiagnosis.Healthy));
    }

    [Fact]
    public void NotifierWaitsAndNotifiesOncePerProblemWhilePluggedIn()
    {
        var n = new ProblemNotifier(TimeSpan.FromSeconds(2));
        var t = new DateTime(2026, 1, 1);
        Assert.Null(n.Update(PhoneLinkProblem.DebuggingOff, false, t));
        Assert.Null(n.Update(PhoneLinkProblem.DebuggingOff, false, t.AddSeconds(1.9)));
        Assert.Equal(PhoneLinkProblem.DebuggingOff, n.Update(PhoneLinkProblem.DebuggingOff, false, t.AddSeconds(2)));
        Assert.Null(n.Update(PhoneLinkProblem.DebuggingOff, false, t.AddSeconds(10)));
        // 다른 문제로 바뀌면 그 시점부터 다시 기다린다 (디버깅을 켰더니 허용 대기)
        Assert.Null(n.Update(PhoneLinkProblem.Unauthorized, false, t.AddSeconds(11)));
        Assert.Equal(PhoneLinkProblem.Unauthorized, n.Update(PhoneLinkProblem.Unauthorized, false, t.AddSeconds(13)));
        // 잠깐 깜빡인 문제는 알리지 않는다
        Assert.Null(n.Update(PhoneLinkProblem.Offline, false, t.AddSeconds(20)));
        Assert.Null(n.Update(PhoneLinkProblem.None, false, t.AddSeconds(21)));
        Assert.Null(n.Update(PhoneLinkProblem.Offline, false, t.AddSeconds(22)));
    }

    [Fact]
    public void NotifierIgnoresUnpluggedPhone()
    {
        // 폰을 꽂지 않은 것은 평소 상태라 알리지 않는다
        var n = new ProblemNotifier(TimeSpan.FromSeconds(2));
        var t = new DateTime(2026, 1, 1);
        n.Update(PhoneLinkProblem.NoDevice, false, t);
        Assert.Null(n.Update(PhoneLinkProblem.NoDevice, false, t.AddSeconds(60)));
    }

    [Fact]
    public void NotifierRearmsWhenCableIsReplugged()
    {
        // 로그 재현: 디버깅 꺼짐 안내 → 선 뽑음 → 다시 꽂음 → 다시 안내해야 한다
        var n = new ProblemNotifier(TimeSpan.FromSeconds(2));
        var t = new DateTime(2026, 1, 1);
        n.Update(PhoneLinkProblem.DebuggingOff, false, t);
        Assert.NotNull(n.Update(PhoneLinkProblem.DebuggingOff, false, t.AddSeconds(3)));
        n.Update(PhoneLinkProblem.NoDevice, false, t.AddSeconds(30));
        n.Update(PhoneLinkProblem.NoDevice, false, t.AddSeconds(35));
        n.Update(PhoneLinkProblem.DebuggingOff, false, t.AddSeconds(45));
        Assert.Equal(PhoneLinkProblem.DebuggingOff, n.Update(PhoneLinkProblem.DebuggingOff, false, t.AddSeconds(47)));
    }

    [Fact]
    public void NotifierCanWaitLongerForOneCall()
    {
        // 도우미 앱이 있으면 꽂은 직후 USB 디버깅이 켜지기까지 PC 쪽에서 2~3초 "꺼짐"으로 보인다. 그동안은 알리지 않는다.
        var n = new ProblemNotifier(TimeSpan.FromSeconds(2));
        var t = new DateTime(2026, 1, 1);
        var longer = TimeSpan.FromSeconds(10);
        Assert.Null(n.Update(PhoneLinkProblem.DebuggingOff, false, t, longer));
        Assert.Null(n.Update(PhoneLinkProblem.DebuggingOff, false, t.AddSeconds(3), longer));
        Assert.Equal(PhoneLinkProblem.DebuggingOff, n.Update(PhoneLinkProblem.DebuggingOff, false, t.AddSeconds(10), longer));
    }

    [Fact]
    public void NotifierRearmsAfterReady()
    {
        var n = new ProblemNotifier(TimeSpan.FromSeconds(2));
        var t = new DateTime(2026, 1, 1);
        n.Update(PhoneLinkProblem.Unauthorized, false, t);
        Assert.NotNull(n.Update(PhoneLinkProblem.Unauthorized, false, t.AddSeconds(3)));
        n.Update(PhoneLinkProblem.None, true, t.AddSeconds(5));
        n.Update(PhoneLinkProblem.Unauthorized, false, t.AddSeconds(6));
        Assert.Equal(PhoneLinkProblem.Unauthorized, n.Update(PhoneLinkProblem.Unauthorized, false, t.AddSeconds(8)));
    }
}
