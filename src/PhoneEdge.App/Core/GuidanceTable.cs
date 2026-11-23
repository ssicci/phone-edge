using PhoneEdge.Contracts;

namespace PhoneEdge.App.Core;

/// <summary>문제 하나에 대한 사용자 안내. 모든 안내 문구는 이 표에만 둔다.</summary>
/// <param name="ShortCause">툴팁에 쓰는 짧은 원인.</param>
/// <param name="Cause">안내 창의 원인 한 줄.</param>
/// <param name="Steps">번호를 붙여 보여 줄 해결 단계.</param>
/// <param name="CanRetry">"다시 시도" 버튼으로 도움이 되는 문제인지.</param>
public sealed record Guidance(string ShortCause, string Cause, string[] Steps, bool CanRetry);

public static class GuidanceTable
{
    private static readonly Dictionary<PhoneLinkProblem, Guidance> Table = new()
    {
        [PhoneLinkProblem.NoDevice] = new(
            "폰이 연결되지 않음",
            "폰이 연결되지 않았거나, 충전만 되는 케이블입니다.",
            new[]
            {
                "케이블을 폰과 노트북에 다시 꽂아 보세요.",
                "데이터 전송이 되는 케이블을 사용하세요 (충전 전용 케이블은 폰이 보이지 않습니다).",
                "노트북의 다른 USB 포트에 꽂아 보세요.",
            }, true),
        [PhoneLinkProblem.DebuggingOff] = new(
            "USB 디버깅 켜기 필요",
            "폰은 USB로 보이지만 USB 디버깅이 꺼져 있습니다.",
            new[]
            {
                "폰에서 설정 → 휴대전화 정보 → 소프트웨어 정보 → 빌드번호를 7번 눌러 개발자 옵션을 켜세요.",
                "설정 → 개발자 옵션 → USB 디버깅을 켜세요.",
                "켤 수 없으면 설정 → 보안 및 개인정보 보호 → 보안 위험 자동 차단을 끄세요.",
                "그래도 안 되면 노트북에 삼성 USB 드라이버를 설치하세요.",
            }, true),
        [PhoneLinkProblem.Unauthorized] = new(
            "USB 디버깅 허용 필요",
            "폰에서 이 노트북을 아직 허용하지 않았습니다.",
            new[]
            {
                "폰의 잠금을 풀고 \"USB 디버깅을 허용하시겠습니까?\" 창을 확인하세요.",
                "\"이 컴퓨터에서 항상 허용\"에 체크하고 허용을 누르세요.",
                "창이 뜨지 않으면 케이블을 뽑았다 다시 꽂으세요.",
                "그래도 안 뜨면 설정 → 개발자 옵션 → USB 디버깅 권한 승인 취소를 누른 뒤 다시 연결하세요.",
            }, true),
        [PhoneLinkProblem.Offline] = new(
            "연결이 불안정함",
            "폰과의 연결이 불안정합니다.",
            new[]
            {
                "케이블을 뽑았다 다시 꽂으세요.",
                "폰의 잠금을 푸세요.",
            }, true),
        [PhoneLinkProblem.MultipleDevices] = new(
            "폰이 여러 대 연결됨",
            "안드로이드 기기가 여러 대 연결되어 어느 폰을 쓸지 정할 수 없습니다.",
            new[]
            {
                "사용할 폰 하나만 연결하고 나머지는 케이블을 빼세요.",
            }, true),
        [PhoneLinkProblem.AdbFailed] = new(
            "adb 실행 실패",
            "adb를 실행하지 못했습니다. 다른 프로그램의 adb와 충돌하거나 보안 프로그램이 막고 있을 수 있습니다.",
            new[]
            {
                "아래 \"다시 시도\"를 눌러 보세요.",
                "Android Studio, Smart Switch, 다른 scrcpy가 켜져 있으면 종료한 뒤 다시 시도하세요.",
                "사내 보안 프로그램이 adb.exe를 차단하고 있지 않은지 확인하세요.",
            }, true),
        [PhoneLinkProblem.ServerFailed] = ServerGuide(),
        [PhoneLinkProblem.InputDeviceFailed] = ServerGuide(),
    };

    private static Guidance ServerGuide() => new(
        "폰 입력 서비스 시작 실패",
        "폰 안의 입력 서비스를 시작하지 못했습니다.",
        new[]
        {
            "아래 \"다시 시도\"를 눌러 보세요.",
            "폰의 설정 → 보안 및 개인정보 보호 → 보안 위험 자동 차단이 꺼져 있는지 확인하세요.",
            "폰을 다시 시작한 뒤 케이블을 다시 연결하세요.",
        }, true);

    /// <summary>문제 없음(None)에는 null.</summary>
    public static Guidance? For(PhoneLinkProblem problem) => Table.GetValueOrDefault(problem);

    public const string PointerAccelerationCause =
        "폰의 포인터 가속이 켜져 있어 마우스를 빠르게 움직이면 노트북으로 돌아오는 시점이 늦어집니다.";

    public const string PointerAccelerationManual =
        "직접 바꾸려면 폰에서 설정 → 일반 → 마우스 및 트랙패드(기기에 따라 이름이 다를 수 있음)에서 포인터 가속을 끄세요.";
}
