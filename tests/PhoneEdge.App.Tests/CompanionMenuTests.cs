using PhoneEdge.App.Core;
using PhoneEdge.Contracts;
using Xunit;

namespace PhoneEdge.App.Tests;

public class CompanionMenuTests
{
    [Fact]
    public void UnknownIsHidden() => Assert.False(CompanionMenuModel.For(CompanionState.Unknown, false).Visible);

    [Fact]
    public void NotInstalledOffersInstall()
    {
        var m = CompanionMenuModel.For(CompanionState.NotInstalled, false);
        Assert.True(m.Visible); Assert.True(m.Enabled); Assert.False(m.Checked);
        Assert.Equal("Menu_CompanionInstall", m.TextKey);
    }

    [Fact]
    public void NeedsUpdateOffersUpdate()
    {
        var m = CompanionMenuModel.For(CompanionState.NeedsUpdate, false);
        Assert.True(m.Visible); Assert.True(m.Enabled); Assert.False(m.Checked);
        Assert.Equal("Menu_CompanionUpdate", m.TextKey);
    }

    [Fact]
    public void InstalledIsDisabledAndChecked()
    {
        var m = CompanionMenuModel.For(CompanionState.Installed, false);
        Assert.True(m.Visible); Assert.False(m.Enabled); Assert.True(m.Checked);
        Assert.Equal("Menu_CompanionInstalled", m.TextKey);
    }

    [Theory]
    [InlineData(CompanionState.NotInstalled)]
    [InlineData(CompanionState.NeedsUpdate)]
    public void BusyDisablesTheItem(CompanionState state)
    {
        var m = CompanionMenuModel.For(state, busy: true);
        Assert.True(m.Visible); Assert.False(m.Enabled);
        Assert.Equal("Menu_CompanionBusy", m.TextKey);
    }

    [Fact]
    public void OnlyTheFirstInstallAsksForConfirmation()
    {
        Assert.True(CompanionMenuModel.NeedsConfirmation(CompanionState.NotInstalled));
        Assert.False(CompanionMenuModel.NeedsConfirmation(CompanionState.NeedsUpdate));
        Assert.False(CompanionMenuModel.NeedsConfirmation(CompanionState.Installed));
    }

    [Fact]
    public async Task FakeInstallEndsInstalledAndNotifiesTheApp()
    {
        var fake = new FakePhoneLink(_ => { });
        await fake.StartAsync(CancellationToken.None);
        int changes = 0;
        fake.DiagnosisChanged += _ => changes++;
        Assert.Equal(CompanionState.NotInstalled, fake.Companion);
        Assert.True(await fake.TryFixAsync(PhoneLinkFix.InstallCompanion));
        Assert.Equal(CompanionState.Installed, fake.Companion);
        Assert.True(changes >= 1);
        fake.SetConnected(false);
        Assert.Equal(CompanionState.Unknown, fake.Companion);   // 폰이 준비되지 않으면 알 수 없음
    }
}
