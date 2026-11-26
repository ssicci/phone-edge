using PhoneEdge.App.Core;

namespace PhoneEdge.App.Native;

/// <summary>노트북 커서 이동 (Win32). 모니터 조회는 <see cref="MonitorProvider"/>.</summary>
internal sealed class Win32Screen : ICursorPlacer
{
    public void PlaceAt(int x, int y) => NativeMethods.SetCursorPos(x, y);
}
