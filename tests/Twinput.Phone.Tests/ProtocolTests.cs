using Twinput.Contracts;
using Twinput.Phone.Protocol;

namespace Twinput.Phone.Tests;

public class ProtocolTests
{
    [Fact]
    public void UhidCreate_matches_scrcpy_5_0_1_layout()
    {
        byte[] desc = [0xAA, 0xBB, 0xCC];
        var m = ControlMessages.UhidCreate(0x0102, 0x0304, 0x0506, "Ab", desc);
        Assert.Equal(new byte[] { 12, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 2, (byte)'A', (byte)'b', 0x00, 0x03, 0xAA, 0xBB, 0xCC }, m);
    }

    [Fact]
    public void UhidCreate_truncates_name_without_splitting_utf8()
    {
        string name = new string('a', 126) + "한"; // 126 + 3 바이트 = 129 > 127
        var m = ControlMessages.UhidCreate(1, 0, 0, name, []);
        Assert.Equal(126, m[7]);
    }

    [Fact]
    public void UhidInput_layout()
    {
        var m = ControlMessages.UhidInput(2, [9, 8, 7]);
        Assert.Equal(new byte[] { 13, 0, 2, 0, 3, 9, 8, 7 }, m);
    }

    [Fact]
    public void UhidDestroy_layout() => Assert.Equal(new byte[] { 14, 0, 1 }, ControlMessages.UhidDestroy(1));

    [Fact]
    public void Mouse_report_is_little_endian_16bit()
    {
        var r = HidReports.Mouse(-2, 300, PhoneMouseButtons.Left | PhoneMouseButtons.Middle, -1);
        Assert.Equal(new byte[] { 0x05, 0xFE, 0xFF, 0x2C, 0x01, 0xFF }, r);
    }

    [Fact]
    public void Mouse_report_carries_back_and_forward_buttons()
    {
        // HID 버튼 4·5 = 리눅스 BTN_SIDE·BTN_EXTRA = 안드로이드 뒤로·앞으로
        var r = HidReports.Mouse(0, 0, PhoneMouseButtons.Back | PhoneMouseButtons.Forward, 0);
        Assert.Equal(0x18, r[0]);
    }

    [Fact]
    public void Mouse_descriptor_declares_five_buttons()
    {
        var d = HidReports.MouseDescriptor.ToArray();
        int i = Array.IndexOf(d, (byte)0x29);                 // Usage Maximum (Button 페이지에서 첫 번째)
        Assert.Equal(5, d[i + 1]);
    }

    [Fact]
    public void Mouse_report_clamps()
    {
        var r = HidReports.Mouse(100000, -100000, PhoneMouseButtons.None, 500);
        Assert.Equal(new byte[] { 0, 0xFF, 0x7F, 0x01, 0x80, 0x7F }, r);
    }

    [Fact]
    public void MouseChunked_splits_large_moves_and_sums_exactly()
    {
        var chunks = HidReports.MouseChunked(-70000, 5, PhoneMouseButtons.Right, 3).ToList();
        Assert.Equal(3, chunks.Count);
        long sx = 0, sy = 0;
        foreach (var c in chunks)
        {
            sx += (short)(c[1] | c[2] << 8);
            sy += (short)(c[3] | c[4] << 8);
            Assert.Equal(2, c[0]);
        }
        Assert.Equal(-70000, sx);
        Assert.Equal(5, sy);
        Assert.Equal(3, chunks[0][5]);
        Assert.Equal(0, chunks[1][5]);
    }

    [Fact]
    public void MouseChunked_zero_move_still_sends_one_report()
        => Assert.Single(HidReports.MouseChunked(0, 0, PhoneMouseButtons.Left, 0));

    [Fact]
    public void Keyboard_report_boot_layout()
    {
        var r = HidReports.Keyboard(0x02, [0x04, 0x90]);
        Assert.Equal(new byte[] { 0x02, 0, 0x04, 0x90, 0, 0, 0, 0 }, r);
    }

    [Fact]
    public void Keyboard_report_takes_at_most_six_keys()
    {
        var r = HidReports.Keyboard(0, [1, 2, 3, 4, 5, 6, 7]);
        Assert.Equal(new byte[] { 0, 0, 1, 2, 3, 4, 5, 6 }, r);
    }

    [Fact]
    public void Descriptors_have_balanced_collections()
    {
        foreach (var d in new[] { HidReports.MouseDescriptor.ToArray(), HidReports.KeyboardDescriptor.ToArray() })
        {
            int depth = 0;
            for (int i = 0; i < d.Length;)
            {
                byte prefix = d[i];
                int size = (prefix & 0x03) == 3 ? 4 : prefix & 0x03;
                if ((prefix & 0xFC) == 0xA0) depth++;
                if (prefix == 0xC0) depth--;
                Assert.True(depth >= 0);
                i += 1 + size;
                Assert.True(i <= d.Length);
            }
            Assert.Equal(0, depth);
        }
    }
}
