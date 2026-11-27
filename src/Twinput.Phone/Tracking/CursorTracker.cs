using Twinput.Contracts;

namespace Twinput.Phone.Tracking;

/// <summary>
/// 상대 이동 UHID 마우스의 폰 커서 위치를 PC 쪽에서 추정한다 (폰 없이 테스트 가능한 순수 로직).
///
/// 방식: "구석 고정 + 클램프 모델 + 이탈 문턱"
/// 1. 진입 때 큰 이동으로 커서를 진입 가장자리의 시작 구석에 밀어붙여 기준을 맞춘다 (안드로이드가 화면 끝에서 클램프).
///    Left·Top → 좌상단, Right → 우상단, Bottom → 좌하단. 그다음 가장자리를 따라 비율 위치까지 옮긴다.
/// 2. 이후 이동은 phone = input × Gain 으로 누적하고 폰과 똑같이 [0, W-1]×[0, H-1] 로 클램프한다.
///    클램프를 흉내 내므로 커서가 벽에 닿을 때마다 오차가 그 축에서 0 으로 리셋된다.
/// 3. 진입 가장자리 벽에 붙은 뒤에도 바깥쪽으로 더 민 양(phone px)이 ExitThreshold 를 넘으면 이탈로 본다.
///    문턱은 남은 추적 오차를 흡수하고, 실수로 살짝 민 것과 "나가려는 의도"를 구분한다.
/// 4. 버튼을 누른 채(드래그 중)에는 이탈하지 않는다.
/// 네 방향은 "이탈 축(가로/세로)"과 "바깥 방향(−/+)" 두 값으로만 다르다.
/// </summary>
public sealed class CursorTracker
{
    /// <summary>입력 카운트 1 이 만드는 폰 픽셀 수 (가속 끈 상태의 실측값으로 맞춘다).</summary>
    public double Gain { get; set; } = 1.0;

    /// <summary>진입 가장자리 벽에 닿은 뒤 바깥쪽으로 더 밀어야 이탈로 보는 양 (폰 px).</summary>
    public double ExitThreshold { get; set; } = 24;

    public int Width => _size[0];
    public int Height => _size[1];
    public double X => _pos[0];
    public double Y => _pos[1];
    public bool Active { get; private set; }

    /// <summary>진입 가장자리. 이탈도 이 가장자리를 바깥으로 넘을 때만 본다.</summary>
    public PhoneEntryEdge Edge { get; private set; } = PhoneEntryEdge.Left;

    private readonly int[] _size = [1080, 2400];
    private readonly double[] _pos = [0, 0];
    private double _pushed; // 진입 가장자리 바깥으로 더 민 양 (폰 px)

    /// <summary>이탈 축: 0 = 가로(Left/Right), 1 = 세로(Top/Bottom).</summary>
    private static int ExitAxis(PhoneEntryEdge e) => e is PhoneEntryEdge.Top or PhoneEntryEdge.Bottom ? 1 : 0;

    /// <summary>바깥 방향: Left/Top 은 −1 (0 쪽 벽), Right/Bottom 은 +1 (끝 쪽 벽).</summary>
    private static int Outward(PhoneEntryEdge e) => e is PhoneEntryEdge.Right or PhoneEntryEdge.Bottom ? 1 : -1;

    private double Ratio(int axis) => _size[axis] <= 1 ? 0 : _pos[axis] / (_size[axis] - 1);

    public double XRatio => Ratio(0);
    public double YRatio => Ratio(1);

    /// <summary>진입 가장자리를 따라가는 비율 (Left/Right: 위→아래, Top/Bottom: 왼쪽→오른쪽).</summary>
    public double EdgeRatio => Ratio(1 - ExitAxis(Edge));

    /// <summary>화면 크기 변경 (접기/펼치기/회전). 반환값: 크기가 실제로 바뀌었는지. 두 축의 비율을 유지한다.</summary>
    public bool SetScreen(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (width == Width && height == Height) return false;
        double xr = XRatio, yr = YRatio;
        _size[0] = width;
        _size[1] = height;
        _pos[0] = Math.Clamp(xr * (width - 1), 0, width - 1);  // 끝 쪽 벽(비율 1)은 새 크기 끝을 따라간다
        _pos[1] = Math.Clamp(yr * (height - 1), 0, height - 1);
        return true;
    }

    /// <summary>
    /// 진입. 폰에 보낼 상대 이동 목록(입력 카운트 단위)을 돌려준다:
    /// 먼저 진입 가장자리의 시작 구석으로 밀어 기준을 맞추고, 그다음 가장자리를 따라 ratio 위치까지 옮긴다.
    /// </summary>
    public IReadOnlyList<(long Dx, long Dy)> Enter(double ratio, PhoneEntryEdge edge = PhoneEntryEdge.Left)
    {
        Edge = Enum.IsDefined(edge) ? edge : PhoneEntryEdge.Left;
        ratio = double.IsNaN(ratio) ? 0.5 : Math.Clamp(ratio, 0, 1);
        int exit = ExitAxis(Edge), along = 1 - exit, outward = Outward(Edge);

        // 가장 큰 변보다 넉넉히 (Gain 이 작아도, 회전 직후여도) 밀어붙인다.
        long home = (long)Math.Ceiling(Math.Max(Width, Height) * 2 / Math.Max(Gain, 0.05));
        long move = (long)Math.Round(ratio * (_size[along] - 1) / Gain);

        _pos[exit] = outward > 0 ? _size[exit] - 1 : 0;
        _pos[along] = Math.Clamp(move * Gain, 0, _size[along] - 1);
        _pushed = 0;
        Active = true;

        var homeMove = new long[2];
        homeMove[exit] = outward * home;
        homeMove[along] = -home; // 가장자리를 따라서는 항상 0 쪽(위·왼쪽)에서 시작
        var moves = new List<(long, long)> { (homeMove[0], homeMove[1]) };
        if (move != 0) moves.Add(along == 0 ? (move, 0) : (0, move));
        return moves;
    }

    public void Leave()
    {
        Active = false;
        _pushed = 0;
    }

    /// <summary>
    /// 이동 한 건을 반영한다. 이탈이면 true 와 이탈 지점의 가장자리 비율(<see cref="EdgeRatio"/>)을 돌려주고 비활성이 된다
    /// (이탈을 일으킨 이동은 폰에 보내지 않아도 된다: 커서는 이미 진입 가장자리 벽에 있다).
    /// </summary>
    public bool Move(int dx, int dy, bool buttonsHeld, out double exitRatio)
    {
        exitRatio = 0;
        if (!Active) return false;

        int exit = ExitAxis(Edge), along = 1 - exit, outward = Outward(Edge);
        int dExit = exit == 0 ? dx : dy, dAlong = exit == 0 ? dy : dx;

        _pos[along] = Math.Clamp(_pos[along] + dAlong * Gain, 0, _size[along] - 1);

        double n = _pos[exit] + dExit * Gain;
        double wall = outward > 0 ? _size[exit] - 1 : 0;
        double beyond = (n - wall) * outward; // 벽 바깥으로 넘친 양
        if (beyond > 0)
        {
            _pushed += beyond;
            _pos[exit] = wall;
        }
        else
        {
            _pos[exit] = Math.Clamp(n, 0, _size[exit] - 1);
            if (dExit * outward < 0) _pushed = 0; // 안쪽으로 돌아서면 밀어붙인 양 초기화
        }

        if (!buttonsHeld && _pushed >= ExitThreshold)
        {
            exitRatio = EdgeRatio;
            Leave();
            return true;
        }
        return false;
    }

    /// <summary>폰에서 읽은 실제 위치로 모델을 맞춘다 (보정용, 선택).</summary>
    public void Resync(double x, double y)
    {
        _pos[0] = Math.Clamp(x, 0, Width - 1);
        _pos[1] = Math.Clamp(y, 0, Height - 1);
    }
}
