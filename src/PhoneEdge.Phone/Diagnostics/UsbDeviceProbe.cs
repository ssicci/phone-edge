using System.Runtime.InteropServices;

namespace PhoneEdge.Phone.Diagnostics;

/// <summary>지금 연결된 USB 장치의 인스턴스 ID 목록 (예: "USB\VID_04E8&amp;PID_6860\R3XX00000AA").</summary>
public interface IUsbDeviceProbe
{
    IReadOnlyList<string> GetPresentUsbDeviceIds();
}

/// <summary>
/// Windows 구성 관리자(cfgmgr32)의 CM_Get_Device_ID_List 로 "USB" 열거자 아래 현재 연결된 장치만 읽는다.
/// 고른 이유: 호출 두 번(크기·목록)으로 수 ms 안에 끝나 폴링 주기마다 돌려도 가볍고, 추가 패키지(WMI)나
/// 별도 프로세스(PowerShell Get-PnpDevice, 1초 이상)가 필요 없다. cfgmgr32 는 Windows 기본 DLL 이라
/// net8.0 대상에서도 P/Invoke 로 쓸 수 있고, Windows 가 아니면 빈 목록을 돌려준다.
/// </summary>
public sealed class WindowsUsbDeviceProbe : IUsbDeviceProbe
{
    private const uint CM_GETIDLIST_FILTER_ENUMERATOR = 0x00000001;
    private const uint CM_GETIDLIST_FILTER_PRESENT = 0x00000100;
    private const int CR_SUCCESS = 0;
    private const int CR_BUFFER_SMALL = 0x1A;

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, EntryPoint = "CM_Get_Device_ID_List_SizeW")]
    private static extern int CM_Get_Device_ID_List_Size(out uint len, string filter, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, EntryPoint = "CM_Get_Device_ID_ListW")]
    private static extern int CM_Get_Device_ID_List(string filter, [Out] char[] buffer, uint len, uint flags);

    public IReadOnlyList<string> GetPresentUsbDeviceIds()
    {
        if (!OperatingSystem.IsWindows()) return [];
        try
        {
            const uint flags = CM_GETIDLIST_FILTER_ENUMERATOR | CM_GETIDLIST_FILTER_PRESENT;
            for (int attempt = 0; attempt < 3; attempt++) // 크기를 잰 사이에 장치가 늘면 다시
            {
                if (CM_Get_Device_ID_List_Size(out uint len, "USB", flags) != CR_SUCCESS) return [];
                var buf = new char[len];
                int cr = CM_Get_Device_ID_List("USB", buf, len, flags);
                if (cr == CR_BUFFER_SMALL) continue;
                if (cr != CR_SUCCESS) return [];
                return new string(buf).Split('\0', StringSplitOptions.RemoveEmptyEntries);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        return [];
    }
}
