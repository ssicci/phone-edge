using PhoneEdge.Contracts;

namespace PhoneEdge.Phone.Protocol;

/// <summary>
/// 가상 입력 장치의 HID 리포트 디스크립터와 리포트 인코더.
/// 각 장치는 별도 UHID 장치라 리포트 ID 를 쓰지 않는다. HID 리포트 안의 정수는 리틀엔디언.
/// </summary>
public static class HidReports
{
    public const ushort KeyboardId = 1;
    public const ushort MouseId = 2;

    /// <summary>
    /// 상대 이동 마우스: 버튼 5개 + 패딩 3비트, X/Y 16비트(-32767~32767), 휠 8비트.
    /// X/Y 를 16비트로 둔 이유: 진입 시 커서를 화면 구석으로 미는 큰 이동을 리포트 한 건으로 보내기 위해.
    /// 리포트 = [buttons][xLo][xHi][yLo][yHi][wheel] (6바이트).
    /// </summary>
    public static ReadOnlySpan<byte> MouseDescriptor =>
    [
        0x05, 0x01,       // Usage Page (Generic Desktop)
        0x09, 0x02,       // Usage (Mouse)
        0xA1, 0x01,       // Collection (Application)
        0x09, 0x01,       //   Usage (Pointer)
        0xA1, 0x00,       //   Collection (Physical)
        0x05, 0x09,       //     Usage Page (Button)
        0x19, 0x01,       //     Usage Minimum (1)
        0x29, 0x05,       //     Usage Maximum (5) — 4: 뒤로(BTN_SIDE), 5: 앞으로(BTN_EXTRA)
        0x15, 0x00,       //     Logical Minimum (0)
        0x25, 0x01,       //     Logical Maximum (1)
        0x95, 0x05,       //     Report Count (5)
        0x75, 0x01,       //     Report Size (1)
        0x81, 0x02,       //     Input (Data, Var, Abs)
        0x95, 0x01,       //     Report Count (1)
        0x75, 0x03,       //     Report Size (3)
        0x81, 0x01,       //     Input (Const) 패딩
        0x05, 0x01,       //     Usage Page (Generic Desktop)
        0x09, 0x30,       //     Usage (X)
        0x09, 0x31,       //     Usage (Y)
        0x16, 0x01, 0x80, //     Logical Minimum (-32767)
        0x26, 0xFF, 0x7F, //     Logical Maximum (32767)
        0x75, 0x10,       //     Report Size (16)
        0x95, 0x02,       //     Report Count (2)
        0x81, 0x06,       //     Input (Data, Var, Rel)
        0x09, 0x38,       //     Usage (Wheel)
        0x15, 0x81,       //     Logical Minimum (-127)
        0x25, 0x7F,       //     Logical Maximum (127)
        0x75, 0x08,       //     Report Size (8)
        0x95, 0x01,       //     Report Count (1)
        0x81, 0x06,       //     Input (Data, Var, Rel)
        0xC0,             //   End Collection
        0xC0,             // End Collection
    ];

    public const int MouseReportSize = 6;
    public const int MaxMouseDelta = 32767;

    /// <summary>
    /// 부트 키보드 호환 리포트 (modifiers, reserved, keys[6]) + LED 출력.
    /// 키 범위는 0x00~0xFF 로 넓혔다 (한/영 LANG1=0x90, 한자 LANG2=0x91 포함).
    /// </summary>
    public static ReadOnlySpan<byte> KeyboardDescriptor =>
    [
        0x05, 0x01,       // Usage Page (Generic Desktop)
        0x09, 0x06,       // Usage (Keyboard)
        0xA1, 0x01,       // Collection (Application)
        0x05, 0x07,       //   Usage Page (Keyboard/Keypad)
        0x19, 0xE0,       //   Usage Minimum (LeftControl)
        0x29, 0xE7,       //   Usage Maximum (RightGUI)
        0x15, 0x00,       //   Logical Minimum (0)
        0x25, 0x01,       //   Logical Maximum (1)
        0x75, 0x01,       //   Report Size (1)
        0x95, 0x08,       //   Report Count (8)
        0x81, 0x02,       //   Input (Data, Var, Abs) modifiers
        0x95, 0x01,       //   Report Count (1)
        0x75, 0x08,       //   Report Size (8)
        0x81, 0x01,       //   Input (Const) reserved
        0x05, 0x08,       //   Usage Page (LEDs)
        0x19, 0x01,       //   Usage Minimum (Num Lock)
        0x29, 0x05,       //   Usage Maximum (Kana)
        0x95, 0x05,       //   Report Count (5)
        0x75, 0x01,       //   Report Size (1)
        0x91, 0x02,       //   Output (Data, Var, Abs)
        0x95, 0x01,       //   Report Count (1)
        0x75, 0x03,       //   Report Size (3)
        0x91, 0x01,       //   Output (Const)
        0x05, 0x07,       //   Usage Page (Keyboard/Keypad)
        0x19, 0x00,       //   Usage Minimum (0)
        0x2A, 0xFF, 0x00, //   Usage Maximum (255)
        0x15, 0x00,       //   Logical Minimum (0)
        0x26, 0xFF, 0x00, //   Logical Maximum (255)
        0x75, 0x08,       //   Report Size (8)
        0x95, 0x06,       //   Report Count (6)
        0x81, 0x00,       //   Input (Data, Array)
        0xC0,             // End Collection
    ];

    public const int KeyboardReportSize = 8;

    public static byte[] Mouse(int dx, int dy, PhoneMouseButtons buttons, int wheel)
    {
        dx = Math.Clamp(dx, -MaxMouseDelta, MaxMouseDelta);
        dy = Math.Clamp(dy, -MaxMouseDelta, MaxMouseDelta);
        wheel = Math.Clamp(wheel, -127, 127);
        var r = new byte[MouseReportSize];
        r[0] = (byte)((byte)buttons & 0x1F);
        r[1] = (byte)dx; r[2] = (byte)(dx >> 8);
        r[3] = (byte)dy; r[4] = (byte)(dy >> 8);
        r[5] = (byte)(sbyte)wheel;
        return r;
    }

    /// <summary>
    /// 큰 이동을 ±32767 이하 리포트 여러 건으로 나눈다. 버튼은 모든 리포트에 같게, 휠은 첫 리포트에만 싣는다.
    /// </summary>
    public static IEnumerable<byte[]> MouseChunked(long dx, long dy, PhoneMouseButtons buttons, int wheel)
    {
        bool first = true;
        do
        {
            int cx = (int)Math.Clamp(dx, -MaxMouseDelta, MaxMouseDelta);
            int cy = (int)Math.Clamp(dy, -MaxMouseDelta, MaxMouseDelta);
            yield return Mouse(cx, cy, buttons, first ? wheel : 0);
            dx -= cx; dy -= cy; first = false;
        } while (dx != 0 || dy != 0);
    }

    public static byte[] Keyboard(byte modifiers, ReadOnlySpan<byte> keys)
    {
        var r = new byte[KeyboardReportSize];
        r[0] = modifiers;
        int n = Math.Min(keys.Length, 6);
        keys[..n].CopyTo(r.AsSpan(2));
        return r;
    }
}
