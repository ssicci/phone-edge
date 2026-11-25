namespace PhoneEdge.Contracts;

/// <summary>
/// 폰이 Ready 가 되지 못하는 원인. 엔진은 원인 판정만 하고, 사용자에게 보여줄 안내 문구는 앱이 가진다.
/// </summary>
public enum PhoneLinkProblem
{
    /// <summary>문제 없음 (Ready 이거나 정상적으로 연결 중).</summary>
    None,
    /// <summary>USB 로 연결된 안드로이드 기기가 전혀 보이지 않음 (선이 빠졌거나 충전 전용 케이블).</summary>
    NoDevice,
    /// <summary>Windows 에는 폰이 USB 로 보이지만 adb 에는 안 보임 (USB 디버깅 꺼짐, 보안 위험 자동 차단, 드라이버).</summary>
    DebuggingOff,
    /// <summary>adb 에 unauthorized 로 보임 (폰의 USB 디버깅 허용 창을 승인하지 않음).</summary>
    Unauthorized,
    /// <summary>adb 에 offline 으로 보임.</summary>
    Offline,
    /// <summary>adb 기기가 둘 이상이라 어느 폰인지 정할 수 없음.</summary>
    MultipleDevices,
    /// <summary>adb 자체가 실행·통신되지 않음 (다른 adb 와 충돌, 보안 프로그램 차단).</summary>
    AdbFailed,
    /// <summary>폰 안의 입력 서버를 올리거나 띄우지 못함.</summary>
    ServerFailed,
    /// <summary>서버는 떴지만 가상 마우스/키보드를 만들지 못함.</summary>
    InputDeviceFailed,
}

/// <summary>동작은 하지만 품질을 떨어뜨리는 폰 설정.</summary>
[Flags]
public enum PhoneLinkWarnings
{
    None = 0,
    /// <summary>폰의 마우스 포인터 가속이 켜져 있어 빠른 이동에서 복귀 시점이 늦어진다.</summary>
    PointerAcceleration = 1,
}

/// <summary>폰 도우미 앱(USB 디버깅을 케이블에 맞춰 켜고 끄는 선택 앱)의 상태.</summary>
public enum CompanionState
{
    /// <summary>아직 확인하지 못함 (폰이 Ready 가 아니거나 확인 전).</summary>
    Unknown,
    /// <summary>폰에 설치되어 있지 않음.</summary>
    NotInstalled,
    /// <summary>설치되어 있지만 PhoneEdge 에 들어 있는 버전보다 낮거나, 권한이 빠져 있음.</summary>
    NeedsUpdate,
    /// <summary>최신 버전이 설치되어 있고 권한도 있음.</summary>
    Installed,
}

/// <summary>앱이 사용자 요청으로 엔진에 맡길 수 있는 자동 조치.</summary>
public enum PhoneLinkFix
{
    /// <summary>즉시 다시 연결을 시도한다 (adb 서버 재시작 포함 여부는 엔진이 판단).</summary>
    Retry,
    /// <summary>폰의 마우스 포인터 가속을 끈다.</summary>
    DisablePointerAcceleration,
    /// <summary>
    /// 폰의 USB 디버깅을 끈다 (은행·증권 앱이 실행을 거부하지 않도록). 성공하면 연결이 끊긴다.
    /// 다시 켜는 것은 폰에서 직접 해야 하므로, 앱 종료처럼 사용자가 끝낼 때만 쓴다.
    /// </summary>
    DisableUsbDebugging,
    /// <summary>
    /// 폰 도우미 앱(PhoneEdge Companion)을 설치하거나 업데이트하고, 필요한 권한과 배터리 예외를 준 뒤 실행한다.
    /// 수십 초 걸릴 수 있다. 결과는 <see cref="IPhoneLink.Companion"/> 갱신으로도 나타난다.
    /// </summary>
    InstallCompanion,
}

/// <summary>
/// 진단 상태 한 건. <paramref name="Detail"/> 은 개발자용 원문(adb 출력, 예외 메시지 등)으로 "자세한 정보"에만 보여준다.
/// </summary>
public sealed record PhoneLinkDiagnosis(PhoneLinkProblem Problem, PhoneLinkWarnings Warnings, string? Detail)
{
    public static readonly PhoneLinkDiagnosis Healthy = new(PhoneLinkProblem.None, PhoneLinkWarnings.None, null);
}
