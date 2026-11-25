using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using PhoneEdge.App.Core;
using PhoneEdge.Contracts;

namespace PhoneEdge.App;

/// <summary>
/// 문서(README)에 쓰는 이미지를 만든다: `PhoneEdge.exe --make-docs docs/images` (scripts/make-docs-images.ps1 참고).
/// 앱 창을 따로 그려서 저장하므로 사용자의 실제 화면은 찍히지 않는다. 폰 화면도 찍지 않는다.
/// 같은 PC·같은 배율에서 다시 실행하면 같은 이미지가 나온다 (예시 데이터만 사용).
/// </summary>
internal static class DocImages
{
    /// <summary>언어마다 하위 폴더(en, ko)에 같은 이름의 이미지를 만든다.</summary>
    public static void Generate(string dir)
    {
        ApplicationConfiguration.Initialize();
        foreach (var lang in new[] { Language.English, Language.Korean })
        {
            L.SetLanguage(lang);
            string d = Path.Combine(dir, lang);
            Directory.CreateDirectory(d);
            TrayIconSheet(Path.Combine(d, "tray-icons.png"), lang);
            TrayMenuImage(Path.Combine(d, "tray-menu.png"));
            GuideImage(Path.Combine(d, "guide-debugging-off.png"), PhoneLinkProblem.DebuggingOff, PhoneLinkWarnings.None);
            GuideImage(Path.Combine(d, "guide-unauthorized.png"), PhoneLinkProblem.Unauthorized, PhoneLinkWarnings.PointerAcceleration);
            UpdateImage(Path.Combine(d, "update-window.png"));
        }
    }

    private static void Save(Bitmap bmp, string path)
    {
        bmp.Save(path, ImageFormat.Png);
        long kb = new FileInfo(path).Length / 1024;
        Console.WriteLine($"{path} ({bmp.Width}x{bmp.Height}, {kb} KB)");
        if (kb > 200) Console.WriteLine("  warning: over 200 KB");
    }

    /// <summary>폼 전체(제목 표시줄 포함)를 그린다. 숨겨진 창이 아니라 화면에 잠깐 떠야 제목 줄까지 그려진다.</summary>
    private static void CaptureForm(Form form, string path)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(20, 20);
        form.Show();
        Application.DoEvents();
        using var bmp = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
        Save(bmp, path);
        form.Close();
        form.Dispose();
    }

    private static void GuideImage(string path, PhoneLinkProblem problem, PhoneLinkWarnings warnings)
    {
        var fake = new FakePhoneLink(_ => { });
        fake.SetDiagnosis(problem, warnings);
        CaptureForm(new GuideForm(fake), path);
    }

    /// <summary>예시 버전과 영문+한글 병기 노트 (CHANGELOG.md 형식과 같다).</summary>
    private const string SampleNotes = """
        Example release notes.

        ### Fixed
        - The cursor no longer jumps when returning to the laptop.

        ---

        예시 릴리스 노트입니다.

        ### 수정
        - 노트북으로 돌아올 때 커서가 튀지 않습니다.
        """;

    private static void UpdateImage(string path)
    {
        var form = new UpdateForm("0.2.0", "0.1.0", SampleNotes, (_, _) => Task.CompletedTask);
        CaptureForm(form, path);
    }

    private static void TrayMenuImage(string path)
    {
        var menu = new TrayMenu("PhoneEdge v0.3.0", installed: true, Language.Auto, () => { }, () => AutostartState.On, () => { },
            () => true, () => { },
            () => { }, () => { }, () => { }, _ => { }, () => { });
        menu.Guide.Enabled = true;
        var strip = menu.Strip;
        strip.CreateControl();
        strip.Size = strip.PreferredSize;
        using var inner = new Bitmap(strip.Width, strip.Height);
        strip.DrawToBitmap(inner, new Rectangle(0, 0, strip.Width, strip.Height));
        // 가장자리에 여백과 테두리를 둔다.
        using var bmp = new Bitmap(inner.Width + 24, inner.Height + 24);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            g.DrawImage(inner, 12, 12);
            using var pen = new Pen(Color.FromArgb(0xB0, 0xB0, 0xB0));
            g.DrawRectangle(pen, 11, 11, inner.Width, inner.Height);
        }
        Save(bmp, path);
        strip.Dispose();
    }

    private static void TrayIconSheet(string path, string lang)
    {
        var kinds = new[] { TrayIconKind.Disconnected, TrayIconKind.Ready, TrayIconKind.Phone, TrayIconKind.Paused, TrayIconKind.Problem };
        var labels = lang == Language.Korean
            ? new[] { "연결 안 됨", "준비됨", "폰 모드", "일시 정지", "문제 있음" }
            : new[] { "Not connected", "Ready", "Phone mode", "Paused", "Problem" };
        const int cellW = 150, cellH = 120;
        using var bmp = new Bitmap(cellW * kinds.Length, cellH * 2);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using var font = new Font(lang == Language.Korean ? "Malgun Gothic" : "Segoe UI", 10f);
        foreach (var (bg, fg, row) in new[] { (Color.White, Color.FromArgb(0x20, 0x20, 0x20), 0), (Color.FromArgb(0x20, 0x20, 0x20), Color.White, 1) })
        {
            using (var b = new SolidBrush(bg)) g.FillRectangle(b, 0, row * cellH, bmp.Width, cellH);
            using var text = new SolidBrush(fg);
            for (int i = 0; i < kinds.Length; i++)
            {
                int x = i * cellW, y = row * cellH;
                using (var big = TrayIcons.Render(kinds[i], 48)) g.DrawImage(big, x + 38, y + 14, 48, 48);
                using (var small = TrayIcons.Render(kinds[i], 16)) g.DrawImage(small, x + 98, y + 30, 16, 16);   // 실제 크기(16px)
                var size = g.MeasureString(labels[i], font);
                g.DrawString(labels[i], font, text, x + (cellW - size.Width) / 2, y + 76);
            }
        }
        Save(bmp, path);
    }
}
