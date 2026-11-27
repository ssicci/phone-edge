using System.Buffers.Binary;
using System.Text;

namespace Twinput.Phone.Protocol;

/// <summary>
/// scrcpy 5.0.1 제어 소켓 메시지 인코더 (PC → 폰).
/// 근거: server/.../control/ControlMessageReader.java, app/src/control_msg.c (태그 v5.0.1).
/// 모든 정수는 빅엔디언.
/// </summary>
public static class ControlMessages
{
    public const byte TypeUhidCreate = 12;
    public const byte TypeUhidInput = 13;
    public const byte TypeUhidDestroy = 14;

    /// <summary>UHID_CREATE 이름 필드 최대 바이트 수 (클라이언트 write_string_tiny 의 max_len).</summary>
    public const int MaxNameBytes = 127;

    /// <summary>type(1) id(2) vendor(2) product(2) nameLen(1) name(n) descLen(2) desc(m)</summary>
    public static byte[] UhidCreate(ushort id, ushort vendorId, ushort productId, string name, ReadOnlySpan<byte> reportDescriptor)
    {
        byte[] nameBytes = TruncateUtf8(name, MaxNameBytes);
        var buf = new byte[1 + 2 + 2 + 2 + 1 + nameBytes.Length + 2 + reportDescriptor.Length];
        int i = 0;
        buf[i++] = TypeUhidCreate;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(i), id); i += 2;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(i), vendorId); i += 2;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(i), productId); i += 2;
        buf[i++] = (byte)nameBytes.Length;
        nameBytes.CopyTo(buf, i); i += nameBytes.Length;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(i), checked((ushort)reportDescriptor.Length)); i += 2;
        reportDescriptor.CopyTo(buf.AsSpan(i));
        return buf;
    }

    /// <summary>type(1) id(2) size(2) data(n)</summary>
    public static byte[] UhidInput(ushort id, ReadOnlySpan<byte> report)
    {
        var buf = new byte[5 + report.Length];
        buf[0] = TypeUhidInput;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(1), id);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(3), checked((ushort)report.Length));
        report.CopyTo(buf.AsSpan(5));
        return buf;
    }

    /// <summary>type(1) id(2)</summary>
    public static byte[] UhidDestroy(ushort id)
    {
        var buf = new byte[3];
        buf[0] = TypeUhidDestroy;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(1), id);
        return buf;
    }

    /// <summary>UTF-8 로 바꾸되 문자 중간에서 자르지 않는다.</summary>
    internal static byte[] TruncateUtf8(string s, int maxBytes)
    {
        byte[] all = Encoding.UTF8.GetBytes(s);
        if (all.Length <= maxBytes) return all;
        int len = maxBytes;
        while (len > 0 && (all[len] & 0xC0) == 0x80) len--; // all[len] 이 이어지는 바이트면 그 문자 시작 전까지 뒤로
        return all.AsSpan(0, len).ToArray();
    }
}
