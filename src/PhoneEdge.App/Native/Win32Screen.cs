using PhoneEdge.App.Core;

namespace PhoneEdge.App.Native;

/// <summary>모니터 배치 조회와 노트북 커서 이동 (Win32).</summary>
internal sealed class Win32Screen : ICursorPlacer
{
    private const int EdgeInset = 8;

    /// <summary>커서가 있는 모니터가 가장 오른쪽(오른쪽 끝이 최대)이면 그 사각형.</summary>
    public ScreenRect? RightEdgeMonitorAt(int x, int y)
    {
        int maxRight = Screen.AllScreens.Max(s => s.Bounds.Right);
        foreach (var s in Screen.AllScreens)
        {
            var b = s.Bounds;
            if (b.Right == maxRight && y >= b.Top && y < b.Bottom && x >= b.Left)
                return new ScreenRect(b.Left, b.Top, b.Right, b.Bottom);
        }
        return null;
    }

    /// <summary>커서가 있는 모니터가 가장 왼쪽(왼쪽 끝이 최소)이면 그 사각형. 오른쪽과 대칭.</summary>
    public ScreenRect? LeftEdgeMonitorAt(int x, int y)
    {
        int minLeft = Screen.AllScreens.Min(s => s.Bounds.Left);
        foreach (var s in Screen.AllScreens)
        {
            var b = s.Bounds;
            if (b.Left == minLeft && y >= b.Top && y < b.Bottom && x < b.Right)
                return new ScreenRect(b.Left, b.Top, b.Right, b.Bottom);
        }
        return null;
    }

    public void PlaceAtLeftEdge(ScreenRect m, double yRatio)
    {
        int y = m.Top + (int)Math.Round(yRatio * Math.Max(0, m.Height - 1));
        NativeMethods.SetCursorPos(m.Left + EdgeInset, y);
    }

    public void PlaceAtRightEdge(ScreenRect m, double yRatio)
    {
        int y = m.Top + (int)Math.Round(yRatio * Math.Max(0, m.Height - 1));
        NativeMethods.SetCursorPos(m.Right - 1 - EdgeInset, y);
    }
}
