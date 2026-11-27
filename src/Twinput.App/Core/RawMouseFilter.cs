namespace Twinput.App.Core;

/// <summary>
/// 경계 감지에 쓸 Raw Input 마우스 이동인지 판단한다. 순수 로직.
/// 장치 핸들(hDevice)은 보지 않는다: PowerToys Mouse Without Borders 같은 도구가 실행 중이면
/// 실제 마우스 입력이 모두 장치 핸들 없이 온다 (v0.5.2 실측: 1996건 모두). 예전처럼 이를 "주입된 입력"으로 버리면
/// 커서가 화면 끝에 닿아도 폰 모드에 들어가지 못한다. 주입 여부는 LL 훅의 플래그로만 판단한다.
/// </summary>
public static class RawMouseFilter
{
    public static bool Accept(bool isMouse, bool isAbsolute, int dx, int dy)
        => isMouse && !isAbsolute && (dx != 0 || dy != 0);
}
