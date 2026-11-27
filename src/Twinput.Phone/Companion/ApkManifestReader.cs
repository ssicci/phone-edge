using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Twinput.Phone.Companion;

public readonly record struct ApkInfo(string Package, long VersionCode, string? VersionName);

/// <summary>
/// Reads package, versionCode and versionName from an APK on the PC without any Android tooling:
/// the APK is a zip, and its AndroidManifest.xml is Android's binary XML (AXML). Only the root
/// &lt;manifest&gt; element is decoded. Chosen over a side-car json so the value can never drift from the APK itself.
/// Format: frameworks/base/libs/androidfw/include/androidfw/ResourceTypes.h (ResChunk_header, ResStringPool_header,
/// ResXMLTree_attrExt, Res_value).
/// </summary>
public static class ApkManifestReader
{
    private const ushort ResStringPoolType = 0x0001;
    private const ushort ResXmlType = 0x0003;
    private const ushort ResXmlResourceMapType = 0x0180;
    private const ushort ResXmlStartElementType = 0x0102;
    private const uint Utf8Flag = 0x100;

    private const uint AttrVersionCode = 0x0101021B; // android:versionCode
    private const uint AttrVersionName = 0x0101021C; // android:versionName

    private const byte TypeString = 0x03;
    private const byte TypeIntDec = 0x10;
    private const byte TypeIntHex = 0x11;

    public static ApkInfo? TryRead(string apkPath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(apkPath);
            var entry = zip.GetEntry("AndroidManifest.xml");
            if (entry == null) return null;
            using var s = entry.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return TryParseManifest(ms.ToArray());
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Parses a binary AndroidManifest.xml. Returns null if it is not AXML or has no package/versionCode.</summary>
    public static ApkInfo? TryParseManifest(ReadOnlySpan<byte> axml)
    {
        if (axml.Length < 8 || U16(axml, 0) != ResXmlType) return null;
        string[] strings = [];
        uint[] resIds = [];
        int pos = U16(axml, 2); // file header size
        while (pos + 8 <= axml.Length)
        {
            ushort type = U16(axml, pos);
            int headerSize = U16(axml, pos + 2);
            int size = (int)U32(axml, pos + 4);
            if (size < 8 || pos + size > axml.Length) return null;
            var chunk = axml.Slice(pos, size);
            switch (type)
            {
                case ResStringPoolType:
                    strings = ReadStringPool(chunk);
                    break;
                case ResXmlResourceMapType:
                    resIds = new uint[(size - headerSize) / 4];
                    for (int i = 0; i < resIds.Length; i++) resIds[i] = U32(chunk, headerSize + i * 4);
                    break;
                case ResXmlStartElementType:
                    // ResXMLTree_node (16) then ResXMLTree_attrExt: ns, name, attributeStart, attributeSize, attributeCount, ...
                    int ext = headerSize;
                    string name = Str(strings, U32(chunk, ext + 4));
                    if (name != "manifest") break;
                    int attrStart = U16(chunk, ext + 8), attrSize = U16(chunk, ext + 10), attrCount = U16(chunk, ext + 12);
                    string? package = null, versionName = null;
                    long? versionCode = null;
                    for (int i = 0; i < attrCount; i++)
                    {
                        int a = ext + attrStart + i * attrSize;
                        uint nameIdx = U32(chunk, a + 4);
                        uint raw = U32(chunk, a + 8);
                        byte dataType = chunk[a + 15];
                        uint data = U32(chunk, a + 16);
                        uint resId = nameIdx < resIds.Length ? resIds[nameIdx] : 0;
                        string attrName = Str(strings, nameIdx);
                        string? text = raw != 0xFFFFFFFF ? Str(strings, raw) : dataType == TypeString ? Str(strings, data) : null;

                        if (resId == AttrVersionCode || (resId == 0 && attrName == "versionCode"))
                            versionCode = dataType is TypeIntDec or TypeIntHex ? data : long.TryParse(text, out long v) ? v : null;
                        else if (resId == AttrVersionName || (resId == 0 && attrName == "versionName"))
                            versionName = text;
                        else if (attrName == "package")
                            package = text;
                    }
                    return package != null && versionCode != null ? new ApkInfo(package, versionCode.Value, versionName) : null;
            }
            pos += size;
        }
        return null;
    }

    private static string[] ReadStringPool(ReadOnlySpan<byte> chunk)
    {
        int count = (int)U32(chunk, 8);
        uint flags = U32(chunk, 16);
        int stringsStart = (int)U32(chunk, 20);
        int headerSize = U16(chunk, 2);
        bool utf8 = (flags & Utf8Flag) != 0;
        var result = new string[count];
        for (int i = 0; i < count; i++)
        {
            int p = stringsStart + (int)U32(chunk, headerSize + i * 4);
            if (utf8)
            {
                p += (chunk[p] & 0x80) != 0 ? 2 : 1;                 // length in characters
                int len = chunk[p] & 0x7F;
                if ((chunk[p] & 0x80) != 0) { len = (len << 8) | chunk[p + 1]; p += 2; } else p += 1;
                result[i] = Encoding.UTF8.GetString(chunk.Slice(p, len));
            }
            else
            {
                int len = U16(chunk, p);
                if ((len & 0x8000) != 0) { len = ((len & 0x7FFF) << 16) | U16(chunk, p + 2); p += 4; } else p += 2;
                result[i] = Encoding.Unicode.GetString(chunk.Slice(p, len * 2));
            }
        }
        return result;
    }

    private static string Str(string[] pool, uint index) => index < pool.Length ? pool[index] : "";
    private static ushort U16(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt16LittleEndian(b[o..]);
    private static uint U32(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b[o..]);
}
