namespace Twinput.Phone.Tracking;

/// <summary>
/// 커서 이득 자동 측정의 판정 (순수 함수).
/// `dumpsys input` 의 호버 위치는 화면이 회전돼 있으면 회전 전(자연 방향) 좌표로 나온다 (폴드7 커버 가로 화면 실측).
/// 그래서 한 축의 절댓값 대신 "측정 이동 전후 두 위치의 거리"로 이득을 구해 회전과 무관하게 만든다.
/// </summary>
public static class Calibration
{
    /// <summary>
    /// 구석에 붙인 위치 <paramref name="before"/> 에서 <paramref name="counts"/> 만큼 한 축으로 옮긴 뒤의 위치 <paramref name="after"/> 로 이득을 구한다.
    /// 이동이 한 축에만 나타나지 않았거나, 0 이거나, 화면 끝(<paramref name="axisLength"/>-1)에 막혔을 수 있으면 null.
    /// </summary>
    public static double? GainFromMove((double X, double Y) before, (double X, double Y) after, int counts, int axisLength)
    {
        if (counts <= 0) return null;
        double dx = Math.Abs(after.X - before.X), dy = Math.Abs(after.Y - before.Y);
        double moved = Math.Max(dx, dy), cross = Math.Min(dx, dy);
        if (moved < 1 || cross > 2) return null;                     // 안 움직였거나 두 축이 함께 움직임 (구석 고정 실패)
        if (axisLength > 0 && moved >= axisLength - 2) return null;   // 벽에 막혔을 수 있음
        return moved / counts;
    }
}
