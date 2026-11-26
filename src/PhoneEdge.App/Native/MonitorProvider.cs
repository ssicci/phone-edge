using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using PhoneEdge.App.Core;

namespace PhoneEdge.App.Native;

/// <summary>
/// 연결된 모니터와 안정적인 ID. ID 는 모니터가 EDID 로 알리는 모델 코드와 시리얼(있으면)에서 만든다.
/// 근거: Windows 의 모니터 장치 경로의 인스턴스 부분이나 GDI 이름(\\.\DISPLAY1)은 꽂은 포트·연결 순서에 따라 바뀌지만,
/// EDID 는 모니터 자체가 담고 있어 포트·재부팅·해상도가 바뀌어도 같다.
/// 같은 모델 둘이 시리얼까지 없으면 구별할 수 없어 순번(#2)을 붙인다. 조회에 실패한 모니터는 GDI 이름으로 대신한다.
/// 시리얼은 해시로만 남겨 settings.json 과 로그에 원본이 나오지 않는다.
/// </summary>
internal static class MonitorProvider
{
    public static List<MonitorInfo> Current()
    {
        var details = TryQueryDetails();
        var list = new List<(string Id, string Sort, ScreenRect Bounds)>();
        foreach (var s in Screen.AllScreens)
        {
            string id, sort;
            if (details.TryGetValue(s.DeviceName, out var d)) { id = d.Id; sort = d.DevicePath; }
            else { id = "gdi:" + s.DeviceName; sort = s.DeviceName; }
            var b = s.Bounds;
            list.Add((id, sort, new ScreenRect(b.Left, b.Top, b.Right, b.Bottom)));
        }
        // 같은 ID 가 둘 이상이면 장치 경로 순으로 순번을 붙인다.
        var result = new List<MonitorInfo>();
        foreach (var g in list.GroupBy(x => x.Id))
        {
            int n = 0;
            foreach (var x in g.OrderBy(x => x.Sort, StringComparer.Ordinal))
                result.Add(new MonitorInfo(++n == 1 ? x.Id : $"{x.Id}#{n}", x.Bounds));
        }
        return result;
    }

    private sealed record Detail(string Id, string DevicePath);

    private static Dictionary<string, Detail> TryQueryDetails()
    {
        var map = new Dictionary<string, Detail>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint np, out uint nm) != 0) return map;
            var paths = new DISPLAYCONFIG_PATH_INFO[np];
            var modes = new DISPLAYCONFIG_MODE_INFO[nm];
            if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref np, paths, ref nm, modes, IntPtr.Zero) != 0) return map;
            for (int i = 0; i < np; i++)
            {
                var src = new DISPLAYCONFIG_SOURCE_DEVICE_NAME { Type = 1, Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(), AdapterId = paths[i].Source.AdapterId, Id = paths[i].Source.Id };
                var tgt = new DISPLAYCONFIG_TARGET_DEVICE_NAME { Type = 2, Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(), AdapterId = paths[i].Target.AdapterId, Id = paths[i].Target.Id };
                if (DisplayConfigGetDeviceInfo(ref src) != 0 || DisplayConfigGetDeviceInfo(ref tgt) != 0) continue;
                map[src.GdiName] = new Detail(BuildId(tgt), tgt.DevicePath);
            }
        }
        catch (Exception ex) { Logger.Info("monitor query failed: " + ex.Message); }
        return map;
    }

    private static string BuildId(DISPLAYCONFIG_TARGET_DEVICE_NAME t)
    {
        // 모델 코드: 장치 경로의 두 번째 조각 (DISPLAY#모델#인스턴스#…). 없으면 EDID 의 제조사·제품 ID.
        string[] parts = t.DevicePath.Split('#');
        string model = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : $"{t.EdidManufactureId:X4}{t.EdidProductCodeId:X4}";
        string serial = ReadEdidSerial(parts) ?? "";
        return serial.Length == 0 ? model : model + ":" + Hash(serial);
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8].ToLowerInvariant();

    /// <summary>레지스트리의 EDID 에서 시리얼. 없거나 읽지 못하면 null.</summary>
    private static string? ReadEdidSerial(string[] devicePathParts)
    {
        try
        {
            if (devicePathParts.Length < 3) return null;
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{devicePathParts[1]}\{devicePathParts[2]}\Device Parameters");
            return key?.GetValue("EDID") is byte[] edid ? EdidSerial(edid) : null;
        }
        catch { return null; }
    }

    /// <summary>EDID 의 시리얼: 0xFF 설명자의 문자열, 없으면 숫자 시리얼(0 이면 없음).</summary>
    internal static string? EdidSerial(byte[] edid)
    {
        if (edid.Length < 128) return null;
        for (int off = 54; off <= 108; off += 18)
        {
            if (edid[off] == 0 && edid[off + 1] == 0 && edid[off + 2] == 0 && edid[off + 3] == 0xFF)
            {
                string text = Encoding.ASCII.GetString(edid, off + 5, 13).Split('\n')[0].Trim();
                if (text.Length > 0) return text;
            }
        }
        uint n = BitConverter.ToUInt32(edid, 12);
        return n != 0 ? n.ToString() : null;
    }

    private const uint QDC_ONLY_ACTIVE_PATHS = 2;

    [StructLayout(LayoutKind.Sequential)] private struct LUID { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_PATH_SOURCE_INFO { public LUID AdapterId; public uint Id, ModeInfoIdx, StatusFlags; }
    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID AdapterId; public uint Id, ModeInfoIdx, OutputTechnology, Rotation, Scaling, RefreshNumerator, RefreshDenominator, ScanLineOrdering;
        public int TargetAvailable; public uint StatusFlags;
    }
    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_PATH_INFO { public DISPLAYCONFIG_PATH_SOURCE_INFO Source; public DISPLAYCONFIG_PATH_TARGET_INFO Target; public uint Flags; }
    [StructLayout(LayoutKind.Sequential, Size = 64)] private struct DISPLAYCONFIG_MODE_INFO { public uint InfoType, Id; public LUID AdapterId; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public uint Type, Size; public LUID AdapterId; public uint Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string GdiName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public uint Type, Size; public LUID AdapterId; public uint Id;
        public uint Flags, OutputTechnology; public ushort EdidManufactureId, EdidProductCodeId; public uint ConnectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string FriendlyName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DevicePath;
    }

    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPaths, out uint numModes);
    [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint numPaths, [Out] DISPLAYCONFIG_PATH_INFO[] paths, ref uint numModes, [Out] DISPLAYCONFIG_MODE_INFO[] modes, IntPtr topology);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME info);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME info);
}
