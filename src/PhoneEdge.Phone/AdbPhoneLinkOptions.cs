namespace PhoneEdge.Phone;

public sealed class AdbPhoneLinkOptions
{
    /// <summary>tools/scrcpy-server 와 정확히 같은 버전이어야 한다 (서버가 버전 불일치 시 종료).</summary>
    public string ServerVersion { get; init; } = "5.0.1";

    /// <summary>폰 안에 올릴 서버 경로. 사용자가 쓰는 scrcpy 와 겹치지 않게 별도 이름.</summary>
    public string DeviceServerPath { get; init; } = "/data/local/tmp/phoneedge-scrcpy-server.jar";

    /// <summary>입력 카운트 1 당 폰 픽셀. 자동 측정이 실패할 때 쓰는 값 (폴드7, 포인터 속도 0 실측 2.68).</summary>
    public double CursorGain { get; init; } = 2.68;

    /// <summary>연결 때 커서 이득을 실측한다 (커서가 좌상단으로 잠깐 움직인다).</summary>
    public bool AutoCalibrate { get; init; } = true;

    /// <summary>이득 측정에 쓰는 이동량 (입력 카운트).</summary>
    public int CalibrationCounts { get; init; } = 200;

    /// <summary>폰 포인터 가속이 켜져 있을 때 진입 이동 사이에 쉬는 시간 (가속이 꺼져 있으면 쓰지 않음).</summary>
    public TimeSpan HomeSettleDelay { get; init; } = TimeSpan.FromMilliseconds(60);

    /// <summary>좌측 벽에서 더 밀어야 이탈로 보는 양 (폰 px).</summary>
    public double ExitThreshold { get; init; } = 24;

    /// <summary>폰을 찾는 주기.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>UHID 장치를 만든 뒤 안드로이드가 장치를 등록할 때까지 기다리는 시간.</summary>
    public TimeSpan DeviceSettleDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>진단 로그. null 이면 Debug 출력.</summary>
    public Action<string>? Log { get; init; }
}
