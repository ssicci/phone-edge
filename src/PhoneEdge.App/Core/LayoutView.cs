namespace PhoneEdge.App.Core;

/// <summary>
/// 설정 창 그림: 화면 좌표(모니터 배치) ↔ 그림 좌표 변환과 모니터 번호. Win32·그리기에 의존하지 않는다.
/// 모니터 배치는 물리 픽셀 좌표라 화면 배율이 모니터마다 달라도 그림은 실제 배치와 같은 비율이다.
/// </summary>
public sealed class LayoutView
{
    public double Scale { get; }
    public double OffsetX { get; }
    public double OffsetY { get; }

    private LayoutView(double scale, double ox, double oy) { Scale = scale; OffsetX = ox; OffsetY = oy; }

    /// <summary>모든 모니터가 margin 을 남기고 width × height 안에 들어오도록 맞추고 가운데에 놓는다.</summary>
    public static LayoutView Fit(IReadOnlyList<MonitorInfo> monitors, double width, double height, double margin)
    {
        if (monitors.Count == 0) return new LayoutView(1, 0, 0);
        int l = monitors.Min(m => m.Bounds.Left), t = monitors.Min(m => m.Bounds.Top);
        int r = monitors.Max(m => m.Bounds.Right), b = monitors.Max(m => m.Bounds.Bottom);
        double w = Math.Max(1, r - l), h = Math.Max(1, b - t);
        double availW = Math.Max(1, width - 2 * margin), availH = Math.Max(1, height - 2 * margin);
        double scale = Math.Min(availW / w, availH / h);
        double ox = margin + (availW - w * scale) / 2 - l * scale;
        double oy = margin + (availH - h * scale) / 2 - t * scale;
        return new LayoutView(scale, ox, oy);
    }

    public double X(double desktopX) => desktopX * Scale + OffsetX;
    public double Y(double desktopY) => desktopY * Scale + OffsetY;
    public double DesktopX(double canvasX) => (canvasX - OffsetX) / Scale;
    public double DesktopY(double canvasY) => (canvasY - OffsetY) / Scale;

    /// <summary>모니터 번호 1.. : 왼쪽에서 오른쪽, 같으면 위에서 아래 순. 설정 창 그림과 "식별" 표시가 같은 번호를 쓴다.</summary>
    public static Dictionary<string, int> Numbers(IReadOnlyList<MonitorInfo> monitors)
    {
        var map = new Dictionary<string, int>();
        int n = 0;
        foreach (var m in monitors.OrderBy(m => m.Bounds.Left).ThenBy(m => m.Bounds.Top).ThenBy(m => m.Id, StringComparer.Ordinal))
            map[m.Id] = ++n;
        return map;
    }
}
