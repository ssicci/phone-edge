using PhoneEdge.App.Core;
using Xunit;

namespace PhoneEdge.App.Tests;

public class HidTests
{
    [Theory]
    [InlineData(0x1E, false, 0x04)]   // A
    [InlineData(0x2C, false, 0x1D)]   // Z
    [InlineData(0x02, false, 0x1E)]   // 1
    [InlineData(0x0B, false, 0x27)]   // 0
    [InlineData(0x1C, false, 0x28)]   // Enter
    [InlineData(0x1C, true, 0x58)]    // 키패드 Enter
    [InlineData(0x39, false, 0x2C)]   // Space
    [InlineData(0x3B, false, 0x3A)]   // F1
    [InlineData(0x58, false, 0x45)]   // F12
    [InlineData(0x48, true, 0x52)]    // ↑
    [InlineData(0x50, true, 0x51)]    // ↓
    [InlineData(0x4B, true, 0x50)]    // ←
    [InlineData(0x4D, true, 0x4F)]    // →
    [InlineData(0x53, true, 0x4C)]    // Delete
    [InlineData(0x1D, false, 0xE0)]   // LCtrl
    [InlineData(0x1D, true, 0xE4)]    // RCtrl
    [InlineData(0x38, false, 0xE2)]   // LAlt
    [InlineData(0x38, true, 0xE6)]    // RAlt
    [InlineData(0x5B, true, 0xE3)]    // LWin
    [InlineData(0xF2, false, 0x90)]   // 한/영 (스캔코드)
    [InlineData(0xF1, false, 0x91)]   // 한자 (스캔코드)
    public void ScanCodeMapsToHidUsage(int scan, bool ext, int usage)
        => Assert.Equal((byte)usage, HidKeyMap.Resolve(0, scan, ext));

    [Fact]
    public void HangulAndHanjaVirtualKeysMapToLang1AndLang2()
    {
        Assert.Equal(HidKeyMap.Lang1, HidKeyMap.Resolve(0x15, 0x38, true));
        Assert.Equal(HidKeyMap.Lang2, HidKeyMap.Resolve(0x19, 0x1D, true));
    }

    [Fact]
    public void UnknownKeyIsZero() => Assert.Equal(0, HidKeyMap.Resolve(0, 0x7F, false));

    [Fact]
    public void KeyboardStateTracksModifiersAndKeys()
    {
        var s = new KeyboardState();
        Assert.True(s.Set(HidKeyMap.LShift, true));
        Assert.True(s.Set(HidKeyMap.RAlt, true));
        Assert.Equal(0b0100_0010, s.Modifiers);
        Assert.True(s.Set(0x04, true));
        Assert.True(s.Set(0x05, true));
        Assert.False(s.Set(0x04, true));
        Assert.Equal(new byte[] { 0x04, 0x05 }, s.Keys.ToArray());
        Assert.True(s.Set(0x04, false));
        Assert.Equal(new byte[] { 0x05 }, s.Keys.ToArray());
        Assert.True(s.Set(HidKeyMap.LShift, false));
        Assert.Equal(0b0100_0000, s.Modifiers);
    }

    [Fact]
    public void KeyboardStateLimitsToSixKeys()
    {
        var s = new KeyboardState();
        for (byte u = 4; u < 10; u++) Assert.True(s.Set(u, true));
        Assert.False(s.Set(10, true));
        Assert.Equal(6, s.Keys.Length);
        s.Clear();
        Assert.Equal(0, s.Keys.Length);
    }
}
