using Twinput.App.Core;
using Twinput.Contracts;
using Xunit;

namespace Twinput.App.Tests;

public class ExitPolicyTests
{
    [Theory]
    [InlineData(ExitReason.UserExit, true)]
    [InlineData(ExitReason.SessionEnding, true)]
    [InlineData(ExitReason.Update, false)]   // 재시작 후 다시 연결해야 한다
    [InlineData(ExitReason.Other, false)]    // 예외·프로세스 종료
    public void OnlyUserExitAndSessionEndingTurnItOff(ExitReason reason, bool expected)
        => Assert.Equal(expected, ExitPolicy.ShouldDisableUsbDebugging(reason, settingEnabled: true, linkReady: true, isInstalled: true, isFakeLink: false));

    [Fact]
    public void NotWhenTheSettingIsOffOrThePhoneIsNotReady()
    {
        Assert.False(ExitPolicy.ShouldDisableUsbDebugging(ExitReason.UserExit, false, true, true, false));
        Assert.False(ExitPolicy.ShouldDisableUsbDebugging(ExitReason.UserExit, true, false, true, false));
        Assert.False(ExitPolicy.ShouldDisableUsbDebugging(ExitReason.SessionEnding, true, false, true, false));
    }

    [Fact]
    public void DevelopmentRunSkipsItUnlessTheLinkIsFake()
    {
        Assert.False(ExitPolicy.ShouldDisableUsbDebugging(ExitReason.UserExit, true, true, isInstalled: false, isFakeLink: false));
        Assert.True(ExitPolicy.ShouldDisableUsbDebugging(ExitReason.UserExit, true, true, isInstalled: false, isFakeLink: true));
    }

    [Fact]
    public void WaitsAtMostThreeSeconds() => Assert.Equal(TimeSpan.FromSeconds(3), ExitPolicy.Timeout);

    [Fact]
    public void SettingDefaultsToOnAndIsSaved()
    {
        string dir = Path.Combine(Path.GetTempPath(), "twinput-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "settings.json");
        try
        {
            var store = new SettingsStore(path);
            Assert.True(store.DisableUsbDebuggingOnExit);
            store.Language = "ko";
            store.DisableUsbDebuggingOnExit = false;
            var again = new SettingsStore(path);
            Assert.False(again.DisableUsbDebuggingOnExit);
            Assert.Equal("ko", again.Language);   // 다른 설정을 지우지 않는다
            again.DisableUsbDebuggingOnExit = true;
            Assert.True(new SettingsStore(path).DisableUsbDebuggingOnExit);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    private sealed class ExitLink : IPhoneLink
    {
        public PhoneLinkState State { get; set; } = PhoneLinkState.Ready;
        public Func<Task<bool>> Fix { get; set; } = () => Task.FromResult(true);
        public int FixCalls;
        public PhoneLinkFix? LastFix;
        public event Action<PhoneLinkState>? StateChanged { add { } remove { } }
        public event Action<double>? ExitRequested { add { } remove { } }
        public Task StartAsync(CancellationToken c) => Task.CompletedTask;
        public Task<bool> TryFixAsync(PhoneLinkFix fix) { FixCalls++; LastFix = fix; return Fix(); }
        public void Enter(double y) { }
        public void Leave() { }
        public void SendMouse(int dx, int dy, PhoneMouseButtons b, int w) { }
        public void SendKeyboard(byte m, ReadOnlySpan<byte> k) { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public void CallsTheFixAndLogsDone()
    {
        var link = new ExitLink();
        var log = new List<string>();
        Assert.True(ExitPolicy.DisableUsbDebuggingIfNeeded(link, ExitReason.UserExit, true, true, log.Add));
        Assert.Equal(PhoneLinkFix.DisableUsbDebugging, link.LastFix);
        Assert.Contains(log, l => l.Contains("done"));
    }

    [Fact]
    public void DoesNotCallItForUpdateRestartOrWhenNotReady()
    {
        var link = new ExitLink();
        Assert.False(ExitPolicy.DisableUsbDebuggingIfNeeded(link, ExitReason.Update, true, true, _ => { }));
        link.State = PhoneLinkState.Disconnected;
        Assert.False(ExitPolicy.DisableUsbDebuggingIfNeeded(link, ExitReason.UserExit, true, true, _ => { }));
        Assert.False(ExitPolicy.DisableUsbDebuggingIfNeeded(new ExitLink(), ExitReason.UserExit, false, true, _ => { }));
        Assert.Equal(0, link.FixCalls);
    }

    [Fact]
    public void SlowFixTimesOutWithoutBlockingExit()
    {
        var link = new ExitLink { Fix = async () => { await Task.Delay(5000); return true; } };
        var log = new List<string>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.True(ExitPolicy.DisableUsbDebuggingIfNeeded(link, ExitReason.SessionEnding, true, true, log.Add, TimeSpan.FromMilliseconds(200)));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2));
        Assert.Contains(log, l => l.Contains("timed out"));
    }

    [Fact]
    public void FailureAndExceptionsAreLoggedNotThrown()
    {
        var failing = new ExitLink { Fix = () => Task.FromResult(false) };
        var log = new List<string>();
        ExitPolicy.DisableUsbDebuggingIfNeeded(failing, ExitReason.UserExit, true, true, log.Add);
        Assert.Contains(log, l => l.Contains("failed"));

        var throwing = new ExitLink { Fix = () => throw new InvalidOperationException("boom") };
        log.Clear();
        ExitPolicy.DisableUsbDebuggingIfNeeded(throwing, ExitReason.UserExit, true, true, log.Add);
        Assert.Contains(log, l => l.Contains("boom"));
    }

    [Fact]
    public void FakeLinkFlowLogsTheCall()
    {
        var fakeLog = new List<string>();
        var fake = new FakePhoneLink(fakeLog.Add);
        fake.StartAsync(CancellationToken.None).Wait();
        var log = new List<string>();
        Assert.True(ExitPolicy.DisableUsbDebuggingIfNeeded(fake, ExitReason.UserExit, true, isInstalled: false, log.Add));   // 개발 실행이어도 가짜 폰이면 흐름을 확인할 수 있다
        Assert.Contains(fakeLog, l => l.Contains("DisableUsbDebugging"));
        Assert.Contains(log, l => l.Contains("done"));
    }
}
