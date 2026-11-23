namespace PhoneEdge.Phone.Tracking;

/// <summary>
/// 상대 이동 UHID 마우스의 폰 커서 위치를 PC 쪽에서 추정한다 (폰 없이 테스트 가능한 순수 로직).
///
/// 방식: "구석 고정 + 클램프 모델 + 이탈 문턱"
/// 1. 진입 때 큰 음수 이동으로 커서를 폰 화면 좌상단 (0,0) 에 밀어붙여 기준을 맞춘다 (안드로이드가 화면 끝에서 클램프).
/// 2. 이후 이동은 phone = input × Gain 으로 누적하고 폰과 똑같이 [0, W-1]×[0, H-1] 로 클램프한다.
///    클램프를 흉내 내므로 커서가 벽에 닿을 때마다 오차가 그 축에서 0 으로 리셋된다.
/// 3. 모델 x 가 0 에 붙은 뒤에도 왼쪽으로 더 민 양(phone px)이 ExitThreshold 를 넘으면 이탈로 본다.
///    문턱은 남은 추적 오차를 흡수하고, 실수로 살짝 왼쪽으로 민 것과 "나가려는 의도"를 구분한다.
/// 4. 버튼을 누른 채(드래그 중)에는 이탈하지 않는다.
/// </summary>
public sealed class CursorTracker
{
    /// <summary>입력 카운트 1 이 만드는 폰 픽셀 수 (가속 끈 상태의 실측값으로 맞춘다).</summary>
    public double Gain { get; set; } = 1.0;

    /// <summary>모델 x 가 0 에 닿은 뒤 왼쪽으로 더 밀어야 이탈로 보는 양 (폰 px).</summary>
    public double ExitThreshold { get; set; } = 24;

    public int Width { get; private set; } = 1080;
    public int Height { get; private set; } = 2400;
    public double X { get; private set; }
    public double Y { get; private set; }
    public bool Active { get; private set; }

    private double _pushedLeft;

    public double YRatio => Height <= 1 ? 0 : Y / (Height - 1);

    /// <summary>화면 크기 변경 (접기/펼치기/회전). 반환값: 크기가 실제로 바뀌었는지.</summary>
    public bool SetScreen(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (width == Width && height == Height) return false;
        Width = width;
        Height = height;
        X = Math.Clamp(X, 0, width - 1);
        Y = Math.Clamp(Y, 0, height - 1);
        return true;
    }

    /// <summary>
    /// 진입. 폰에 보낼 상대 이동 목록(입력 카운트 단위)을 돌려준다:
    /// 먼저 좌상단으로 밀어 (0,0) 기준을 맞추고, 그다음 아래로 yRatio 위치까지 내린다.
    /// </summary>
    public IReadOnlyList<(long Dx, long Dy)> Enter(double yRatio)
    {
        yRatio = double.IsNaN(yRatio) ? 0.5 : Math.Clamp(yRatio, 0, 1);
        // 가장 큰 변보다 넉넉히 (Gain 이 작아도, 회전 직후여도) 밀어붙인다.
        long home = (long)Math.Ceiling(Math.Max(Width, Height) * 2 / Math.Max(Gain, 0.05));
        long down = (long)Math.Round(yRatio * (Height - 1) / Gain);

        X = 0;
        Y = Math.Clamp(down * Gain, 0, Height - 1);
        _pushedLeft = 0;
        Active = true;

        var moves = new List<(long, long)> { (-home, -home) };
        if (down != 0) moves.Add((0, down));
        return moves;
    }

    public void Leave()
    {
        Active = false;
        _pushedLeft = 0;
    }

    /// <summary>
    /// 이동 한 건을 반영한다. 이탈이면 true 와 이탈 높이 비율을 돌려주고 비활성이 된다
    /// (이탈을 일으킨 이동은 폰에 보내지 않아도 된다: 커서는 이미 좌측 벽에 있다).
    /// </summary>
    public bool Move(int dx, int dy, bool buttonsHeld, out double exitYRatio)
    {
        exitYRatio = 0;
        if (!Active) return false;

        double nx = X + dx * Gain;
        Y = Math.Clamp(Y + dy * Gain, 0, Height - 1);

        if (nx < 0)
        {
            _pushedLeft += -nx;
            X = 0;
        }
        else
        {
            X = Math.Min(nx, Width - 1);
            if (dx > 0) _pushedLeft = 0; // 오른쪽으로 돌아서면 밀어붙인 양 초기화
        }

        if (!buttonsHeld && _pushedLeft >= ExitThreshold)
        {
            exitYRatio = YRatio;
            Leave();
            return true;
        }
        return false;
    }

    /// <summary>폰에서 읽은 실제 위치로 모델을 맞춘다 (보정용, 선택).</summary>
    public void Resync(double x, double y)
    {
        X = Math.Clamp(x, 0, Width - 1);
        Y = Math.Clamp(y, 0, Height - 1);
    }
}
