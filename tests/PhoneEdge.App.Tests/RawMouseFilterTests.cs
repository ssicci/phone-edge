using PhoneEdge.App.Core;
using Xunit;

namespace PhoneEdge.App.Tests;

public class RawMouseFilterTests
{
    /// <summary>
    /// v0.5.2 회귀: Mouse Without Borders 가 실행 중이면 실제 마우스 이동이 장치 핸들 없이 온다.
    /// 필터는 장치 핸들을 입력으로 받지 않으므로 이런 이동도 받아들여야 한다.
    /// </summary>
    [Fact]
    public void AcceptsRelativeMovesRegardlessOfDeviceHandle()
        => Assert.True(RawMouseFilter.Accept(isMouse: true, isAbsolute: false, dx: 30, dy: -16));

    [Theory]
    [InlineData(false, false, 5, 0)]   // 키보드 등 마우스가 아님
    [InlineData(true, true, 5, 0)]     // 절대 좌표 (원격 데스크톱, 펜)
    [InlineData(true, false, 0, 0)]    // 이동 없음 (버튼·휠만)
    public void RejectsOtherInput(bool isMouse, bool isAbsolute, int dx, int dy)
        => Assert.False(RawMouseFilter.Accept(isMouse, isAbsolute, dx, dy));
}
