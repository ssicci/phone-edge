using Microsoft.Win32;
using PhoneEdge.App.Core;
using Xunit;

namespace PhoneEdge.App.Tests;

/// <summary>
/// 실제 Run 키가 아니라 HKCU\Software\PhoneEdgeTests\{guid} 아래의 테스트 키에서만 동작한다.
/// </summary>
public sealed class AutostartTests : IDisposable
{
    private const string Launcher = @"C:\Users\someone\AppData\Local\PhoneEdge\PhoneEdge.exe";
    private readonly string _root = @"Software\PhoneEdgeTests\" + Guid.NewGuid().ToString("N");
    private string RunKey => _root + @"\Run";
    private string ApprovedKey => _root + @"\StartupApproved\Run";
    private Autostart Create(string launcher = Launcher) => new(launcher, RunKey, ApprovedKey);

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_root, throwOnMissingSubKey: false);
        // 다른 테스트가 아직 쓰고 있지 않으면 빈 부모 키도 지워 사용자 레지스트리에 흔적을 남기지 않는다
        try
        {
            using var parent = Registry.CurrentUser.OpenSubKey(@"Software\PhoneEdgeTests");
            if (parent is { SubKeyCount: 0, ValueCount: 0 }) Registry.CurrentUser.DeleteSubKey(@"Software\PhoneEdgeTests", throwOnMissingSubKey: false);
        }
        catch { }
    }

    private string? RunValue()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue(Autostart.ValueName) as string;
    }

    private void SetRun(string value)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        k.SetValue(Autostart.ValueName, value);
    }

    private void SetApproved(byte first)
    {
        using var k = Registry.CurrentUser.CreateSubKey(ApprovedKey);
        var data = new byte[12];
        data[0] = first;
        k.SetValue(Autostart.ValueName, data, RegistryValueKind.Binary);
    }

    private bool HasApproved()
    {
        using var k = Registry.CurrentUser.OpenSubKey(ApprovedKey);
        return k?.GetValue(Autostart.ValueName) != null;
    }

    [Fact]
    public void IsOffByDefault()
    {
        Assert.Equal(AutostartState.Off, Create().GetState());
        Assert.False(Create().IsEnabled);
    }

    [Fact]
    public void EnableWritesTheQuotedLauncherPath()
    {
        var a = Create();
        a.Enable();
        Assert.Equal("\"" + Launcher + "\"", RunValue());
        Assert.Equal(AutostartState.On, a.GetState());
    }

    [Fact]
    public void DisableRemovesTheRunValue()
    {
        var a = Create();
        a.Enable();
        a.Disable();
        Assert.Null(RunValue());
        Assert.Equal(AutostartState.Off, a.GetState());
        a.Disable();   // 이미 꺼져 있어도 오류 없음
    }

    [Fact]
    public void RunValuePointingElsewhereIsNotOnAndEnableFixesIt()
    {
        SetRun("\"C:\\Old\\Place\\PhoneEdge.exe\"");
        var a = Create();
        Assert.Equal(AutostartState.OtherPath, a.GetState());
        Assert.False(a.IsEnabled);
        a.Enable();
        Assert.Equal("\"" + Launcher + "\"", RunValue());
        Assert.Equal(AutostartState.On, a.GetState());
    }

    [Fact]
    public void CurrentFolderExeIsNotTheLauncher()
    {
        SetRun("\"" + Launcher.Replace("PhoneEdge.exe", @"current\PhoneEdge.exe") + "\"");
        Assert.Equal(AutostartState.OtherPath, Create().GetState());
    }

    [Fact]
    public void PathComparisonIgnoresCaseAndAcceptsUnquotedValues()
    {
        SetRun(Launcher.ToUpperInvariant().Replace(" ", ""));
        Assert.Equal(AutostartState.On, Create().GetState());
        SetRun("\"" + Launcher.ToLowerInvariant() + "\"");
        Assert.Equal(AutostartState.On, Create().GetState());
    }

    [Theory]
    [InlineData((byte)3)]
    [InlineData((byte)7)]
    public void DisabledInWindowsIsNotOnAndEnableClearsTheRecord(byte first)
    {
        var a = Create();
        SetRun("\"" + Launcher + "\"");
        SetApproved(first);
        Assert.Equal(AutostartState.DisabledInWindows, a.GetState());
        Assert.False(a.IsEnabled);
        a.Enable();
        Assert.False(HasApproved());
        Assert.Equal(AutostartState.On, a.GetState());
    }

    [Theory]
    [InlineData((byte)2)]
    [InlineData((byte)6)]
    public void ApprovedRecordMeansOn(byte first)
    {
        SetRun("\"" + Launcher + "\"");
        SetApproved(first);
        Assert.Equal(AutostartState.On, Create().GetState());
    }

    [Fact]
    public void StatusIsReadAgainEveryTime()
    {
        var a = Create();
        a.Enable();
        Assert.True(a.IsEnabled);
        SetApproved(3);                 // 사용자가 Windows 설정에서 껐다
        Assert.False(a.IsEnabled);
        SetApproved(2);                 // 다시 켰다
        Assert.True(a.IsEnabled);
    }

    [Fact]
    public void RemoveClearsBothValues()
    {
        var a = Create();
        a.Enable();
        SetApproved(3);
        a.Remove();
        Assert.Null(RunValue());
        Assert.False(HasApproved());
        Assert.Equal(AutostartState.Off, a.GetState());
        a.Remove();   // 아무것도 없어도 오류 없음
    }

    [Fact]
    public void DisableKeepsOtherRunValues()
    {
        using (var k = Registry.CurrentUser.CreateSubKey(RunKey)) k.SetValue("Other", "x");
        var a = Create();
        a.Enable();
        a.Remove();
        using var run = Registry.CurrentUser.OpenSubKey(RunKey);
        Assert.Equal("x", run!.GetValue("Other"));
    }
}
