namespace Twinput.App.Core;

/// <summary>눌린 키 집합. HID 부트 키보드 리포트(수정키 비트마스크 + 최대 6키)로 내보낸다.</summary>
public sealed class KeyboardState
{
    private readonly byte[] _keys = new byte[6];
    private int _count;

    public byte Modifiers { get; private set; }
    public ReadOnlySpan<byte> Keys => _keys.AsSpan(0, _count);

    /// <summary>usage 0xE0~0xE7 은 수정키로 취급한다. 상태가 바뀌었으면 true (자동 반복 down 은 false).</summary>
    public bool Set(byte usage, bool down)
    {
        if (usage == 0) return false;
        if (usage >= 0xE0 && usage <= 0xE7)
        {
            byte bit = (byte)(1 << (usage - 0xE0));
            byte next = down ? (byte)(Modifiers | bit) : (byte)(Modifiers & ~bit);
            if (next == Modifiers) return false;
            Modifiers = next;
            return true;
        }

        int idx = Array.IndexOf(_keys, usage, 0, _count);
        if (down)
        {
            if (idx >= 0 || _count == _keys.Length) return false;
            _keys[_count++] = usage;
            return true;
        }
        if (idx < 0) return false;
        Array.Copy(_keys, idx + 1, _keys, idx, _count - idx - 1);
        _count--;
        return true;
    }

    public void Clear()
    {
        Modifiers = 0;
        _count = 0;
    }
}
