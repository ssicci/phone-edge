namespace PhoneEdge.Contracts;

/// <summary>전송 통로의 연결 상태.</summary>
public enum PhoneLinkState
{
    /// <summary>폰이 없거나 끊김. 입력을 보낼 수 없다.</summary>
    Disconnected,
    /// <summary>폰을 찾았고 가상 입력 장치를 준비하는 중.</summary>
    Connecting,
    /// <summary>가상 입력 장치가 준비됨. 폰 모드에 들어갈 수 있다.</summary>
    Ready,
}

[Flags]
public enum PhoneMouseButtons : byte
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 4,
    /// <summary>마우스 뒤로가기(측면) 버튼. Windows XBUTTON1, 안드로이드에서 뒤로 동작.</summary>
    Back = 8,
    /// <summary>마우스 앞으로가기(측면) 버튼. Windows XBUTTON2.</summary>
    Forward = 16,
}

/// <summary>
/// 전송 통로 하나. 앱(훅·경계 감지)과 폰 연결 엔진 사이의 유일한 접점이다.
/// 폰 화면 크기, 접힘/회전, 폰 커서 위치 추적은 모두 구현 안에 숨긴다.
///
/// 스레드 규칙:
/// - Send*/Enter/Leave 는 입력 훅 스레드에서 호출되므로 즉시 반환해야 한다 (내부 큐 사용, 블로킹 I/O 금지).
/// - 이벤트는 임의의 스레드에서 발생할 수 있다. 구독자가 UI 스레드로 옮긴다.
/// - State 가 Ready 가 아닐 때의 Send*/Enter/Leave 호출은 예외 없이 무시한다.
/// </summary>
public interface IPhoneLink : IAsyncDisposable
{
    PhoneLinkState State { get; }

    /// <summary>상태가 바뀔 때 발생. Ready 에서 벗어나면 앱은 비상 복귀한다.</summary>
    event Action<PhoneLinkState>? StateChanged;

    /// <summary>
    /// 폰 커서가 진입 경계를 넘어 나가려 할 때 발생 (노트북 모드로 복귀 요청).
    /// 인자는 나간 지점의 높이 비율: 0.0 = 화면 맨 위, 1.0 = 맨 아래.
    /// 발생 후 구현은 스스로 Leave 상태가 된다.
    /// </summary>
    event Action<double>? ExitRequested;

    /// <summary>
    /// 현재 진단. State 가 Ready 가 아닐 때 왜 아닌지, Ready 여도 어떤 경고가 있는지 알려준다.
    /// 같은 원인이 반복 판정돼도 값이 바뀔 때만 DiagnosisChanged 를 발생시킨다.
    /// </summary>
    PhoneLinkDiagnosis Diagnosis => PhoneLinkDiagnosis.Healthy;

    event Action<PhoneLinkDiagnosis>? DiagnosisChanged { add { } remove { } }

    /// <summary>
    /// 자동 조치를 수행한다. 오래 걸릴 수 있으므로 UI 스레드에서 await 한다 (훅 스레드에서 호출 금지).
    /// 수행했으면 true, 지원하지 않거나 실패하면 false. 결과는 Diagnosis 갱신으로도 나타난다.
    /// </summary>
    Task<bool> TryFixAsync(PhoneLinkFix fix) => Task.FromResult(false);

    /// <summary>
    /// 폰 감시와 연결을 시작한다. 폰이 없어도 실패하지 않고, 연결·끊김·재연결을
    /// StateChanged 로 알리며 Dispose 될 때까지 계속 동작한다.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 폰 모드 진입. 폰 커서를 진입 경계(1단계에서는 폰 화면의 좌측 가장자리)의
    /// 높이 비율 <paramref name="yRatio"/> (0.0~1.0) 위치에 놓는다.
    /// </summary>
    void Enter(double yRatio);

    /// <summary>폰 모드 종료. 눌려 있는 버튼과 키를 모두 뗀 상태로 만든다.</summary>
    void Leave();

    /// <summary>
    /// 마우스 입력 한 건.
    /// <paramref name="dx"/>, <paramref name="dy"/>: 원시 마우스 이동량 (오른쪽·아래가 양수).
    /// <paramref name="buttons"/>: 지금 눌려 있는 버튼 전체 (변화분이 아님).
    /// <paramref name="wheel"/>: 휠 칸 수 (위로 굴리면 양수, 없으면 0).
    /// </summary>
    void SendMouse(int dx, int dy, PhoneMouseButtons buttons, int wheel);

    /// <summary>
    /// 키보드 상태 전체 (변화분이 아님). HID 부트 키보드 리포트와 같은 의미다.
    /// <paramref name="modifiers"/>: 비트 0~7 = LCtrl, LShift, LAlt, LGui, RCtrl, RShift, RAlt, RGui.
    /// <paramref name="keys"/>: 지금 눌려 있는 키의 HID usage 코드 (Keyboard/Keypad 페이지 0x07), 최대 6개.
    /// </summary>
    void SendKeyboard(byte modifiers, ReadOnlySpan<byte> keys);
}
