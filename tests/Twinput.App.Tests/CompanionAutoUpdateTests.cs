using Twinput.App.Core;
using Twinput.Contracts;
using Xunit;

namespace Twinput.App.Tests;

/// <summary>
/// 이미 설치된 도우미 앱은 Twinput 가 새 버전을 가지고 있으면 묻지 않고 업데이트한다 (사용자 결정, 2026-11-25).
/// 처음 설치는 여전히 확인을 받으므로 자동으로 하지 않는다.
/// </summary>
public class CompanionAutoUpdateTests
{
    [Fact]
    public void UpdatesAnInstalledCompanionOncePerConnection()
    {
        Assert.True(CompanionMenuModel.ShouldAutoUpdate(CompanionState.NeedsUpdate, ready: true, busy: false, phoneMode: false, triedThisConnection: false));
        Assert.False(CompanionMenuModel.ShouldAutoUpdate(CompanionState.NeedsUpdate, ready: true, busy: false, phoneMode: false, triedThisConnection: true));
    }

    [Theory]
    [InlineData(CompanionState.NotInstalled)]   // 처음 설치는 사용자가 메뉴에서 확인하고 한다
    [InlineData(CompanionState.Installed)]
    [InlineData(CompanionState.Unknown)]
    public void LeavesOtherStatesAlone(CompanionState state)
        => Assert.False(CompanionMenuModel.ShouldAutoUpdate(state, ready: true, busy: false, phoneMode: false, triedThisConnection: false));

    [Fact]
    public void WaitsWhileBusyNotReadyOrInPhoneMode()
    {
        // 설치 중에는 연결이 잠깐 흔들려 폰 모드에서 튕겨 나올 수 있으므로, 폰을 쓰는 동안에는 미룬다
        Assert.False(CompanionMenuModel.ShouldAutoUpdate(CompanionState.NeedsUpdate, ready: true, busy: false, phoneMode: true, triedThisConnection: false));
        Assert.False(CompanionMenuModel.ShouldAutoUpdate(CompanionState.NeedsUpdate, ready: false, busy: false, phoneMode: false, triedThisConnection: false));
        Assert.False(CompanionMenuModel.ShouldAutoUpdate(CompanionState.NeedsUpdate, ready: true, busy: true, phoneMode: false, triedThisConnection: false));
    }
}
