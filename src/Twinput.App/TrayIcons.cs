using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Twinput.App.Core;

namespace Twinput.App;

/// <summary>
/// 폰 실루엣 + 상태 표시. 요청한 픽셀 크기로 직접 그려서 16~32px 에서 선명하다.
/// 밝은/어두운 작업 표시줄 모두에서 보이도록 어두운 몸체에 밝은 테두리를 둔다.
/// 폰 모드는 파란 몸체와 화살표, 나머지는 우하단 배지 모양(체크·막대·느낌표)으로도 구분한다.
/// </summary>
internal static class TrayIcons
{
    private static readonly Dictionary<(TrayIconKind, int), Icon> Cache = new();

    public static Color BadgeColor(TrayIconKind kind) => kind switch
    {
        TrayIconKind.Ready => Color.FromArgb(0x2E, 0xB8, 0x4B),
        TrayIconKind.Phone => Color.FromArgb(0x1E, 0x88, 0xE5),
        TrayIconKind.Paused => Color.FromArgb(0xF5, 0x9E, 0x0B),
        TrayIconKind.Problem => Color.FromArgb(0xE5, 0x39, 0x35),
        _ => Color.FromArgb(0x8A, 0x8F, 0x98),
    };

    /// <summary>트레이용 현재 배율의 작은 아이콘 크기.</summary>
    public static int TraySize => Math.Max(16, SystemInformation.SmallIconSize.Width);

    public static Icon Get(TrayIconKind kind, int size)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue((kind, size), out var icon))
            {
                using var bmp = Render(kind, size);
                icon = Icon.FromHandle(bmp.GetHicon());   // 종류×크기별 5개 이내라 핸들을 캐시로 유지한다
                Cache[(kind, size)] = icon;
            }
            return icon;
        }
    }

    public static Bitmap Render(TrayIconKind kind, int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float s = size;
        float line = Math.Max(1f, s / 16f);
        var badge = BadgeColor(kind);
        bool phoneMode = kind == TrayIconKind.Phone;

        // 폰 몸체
        var body = new RectangleF(s * 0.20f, s * 0.04f, s * 0.46f, s * 0.92f);
        using (var path = RoundRect(body, s * 0.10f))
        using (var fill = new SolidBrush(phoneMode ? badge : Color.FromArgb(0x2B, 0x33, 0x40)))
        using (var edge = new Pen(Color.FromArgb(0xF2, 0xF2, 0xF2), line))
        {
            g.FillPath(fill, path);
            g.DrawPath(edge, path);
        }

        // 화면
        var screen = new RectangleF(body.X + s * 0.07f, body.Y + s * 0.12f, body.Width - s * 0.14f, body.Height - s * 0.24f);
        if (!phoneMode)
        {
            using var sb = new SolidBrush(Color.FromArgb(0x9F, 0xB3, 0xCC));
            g.FillRectangle(sb, screen);
        }

        if (phoneMode)
        {
            // 화면을 가로지르는 오른쪽 → 왼쪽(폰 쪽으로 들어오는) 화살표: 노트북 오른쪽에서 폰으로 넘어옴을 뜻한다
            float cy = body.Y + body.Height / 2;
            using var pen = new Pen(Color.White, Math.Max(1.5f, s / 9f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            float x0 = s * 0.80f, x1 = s * 0.30f, h = s * 0.14f;
            g.DrawLine(pen, x0, cy, x1, cy);
            g.DrawLines(pen, new[] { new PointF(x1 + h, cy - h), new PointF(x1, cy), new PointF(x1 + h, cy + h) });
        }
        else
        {
            // 우하단 배지
            float r = s * 0.29f, cx = s - r - line * 0.5f, cy = s - r - line * 0.5f;
            using (var bb = new SolidBrush(badge))
            using (var ring = new Pen(Color.White, line))
            {
                g.FillEllipse(bb, cx - r, cy - r, 2 * r, 2 * r);
                g.DrawEllipse(ring, cx - r, cy - r, 2 * r, 2 * r);
            }
            using var mark = new Pen(Color.White, Math.Max(1f, s / 13f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var white = new SolidBrush(Color.White);
            switch (kind)
            {
                case TrayIconKind.Ready:   // 체크
                    g.DrawLines(mark, new[] { new PointF(cx - r * 0.5f, cy), new PointF(cx - r * 0.1f, cy + r * 0.4f), new PointF(cx + r * 0.5f, cy - r * 0.4f) });
                    break;
                case TrayIconKind.Paused:  // 일시 정지 막대 두 개
                    g.DrawLine(mark, cx - r * 0.32f, cy - r * 0.45f, cx - r * 0.32f, cy + r * 0.45f);
                    g.DrawLine(mark, cx + r * 0.32f, cy - r * 0.45f, cx + r * 0.32f, cy + r * 0.45f);
                    break;
                case TrayIconKind.Problem: // 느낌표
                    g.DrawLine(mark, cx, cy - r * 0.55f, cx, cy + r * 0.1f);
                    g.FillEllipse(white, cx - mark.Width * 0.5f, cy + r * 0.38f, mark.Width, mark.Width);
                    break;
                default:                   // 연결 안 됨: 가로선
                    g.DrawLine(mark, cx - r * 0.45f, cy, cx + r * 0.45f, cy);
                    break;
            }
        }
        return bmp;
    }

    private static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>exe 아이콘용 .ico (PNG 압축, 여러 크기). `Twinput.exe --make-ico 경로` 로 생성한다.</summary>
    public static void WriteIco(string path, TrayIconKind kind)
    {
        int[] sizes = { 16, 20, 24, 32, 48, 64, 256 };
        var pngs = new List<byte[]>();
        foreach (var size in sizes)
        {
            using var bmp = Render(kind, size);
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            pngs.Add(ms.ToArray());
        }
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
            w.Write(pngs[i].Length); w.Write(offset);
            offset += pngs[i].Length;
        }
        foreach (var p in pngs) w.Write(p);
    }
}
