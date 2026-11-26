using System.Windows.Forms;
using PhoneEdge.App.Core;
using Xunit;

namespace PhoneEdge.App.Tests;

[Collection("UiLanguage")]
public class PhoneSlotFormTests
{
    private static readonly MonitorInfo A = new("AAA0001:11111111", new ScreenRect(0, 0, 1920, 1080));
    private static readonly MonitorInfo B = new("BBB0002:22222222", new ScreenRect(1920, 0, 3840, 1080));

    private static void Sta(Action body)
    {
        Exception? error = null;
        var t = new Thread(() => { try { body(); } catch (Exception ex) { error = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        Assert.Null(error);
    }

    private static Button? Find(Control root, string text)
    {
        foreach (Control c in root.Controls)
        {
            if (c is Button b && b.Text == text) return b;
            if (Find(c, text) is { } inner) return inner;
        }
        return null;
    }

    [Fact]
    public void StartsWithTheCurrentSlotAndSavesIt()
    {
        Sta(() =>
        {
            var current = new SlotCandidate(B, PhoneSide.Right, new Span(0, 1080)).ToResolved();
            using var form = new PhoneSlotForm(new[] { A, B }, current) { Opacity = 0, ShowInTaskbar = false };
            Assert.Equal((B.Id, PhoneSide.Right), (form.Selected!.Monitor.Id, form.Selected.Side));
            form.Show();
            Application.DoEvents();
            Find(form, L.T("Slot_Save"))!.PerformClick();
            Assert.Equal(PhoneSide.Right, form.Result!.Side);
            Assert.Equal(new Span(0, 1080), form.Result.Span);
        });
    }

    [Fact]
    public void CancelLeavesNoResultAndTheTextsFollowTheLanguage()
    {
        Sta(() =>
        {
            var old = L.Lang;
            try
            {
                L.SetLanguage(Language.Korean);
                using var form = new PhoneSlotForm(new[] { A, B }, null, firstTime: true) { Opacity = 0, ShowInTaskbar = false };
                form.Show();
                Application.DoEvents();
                Assert.NotNull(Find(form, "취소"));
                Assert.NotNull(Find(form, "식별"));
                L.SetLanguage(Language.English);
                form.ApplyLanguage();
                Assert.NotNull(Find(form, "Identify"));
                Find(form, "Cancel")!.PerformClick();
                Assert.Null(form.Result);
            }
            finally { L.SetLanguage(old); }
        });
    }

    [Fact]
    public void RendersWithoutThrowingOnMixedScalesAndStackedMonitors()
    {
        Sta(() =>
        {
            // 위·아래로 쌓인 모니터와 해상도가 다른 모니터 (화면 배율이 달라도 좌표는 물리 픽셀)
            var c = new MonitorInfo("CCC0003", new ScreenRect(-3840, -400, 0, 1760));
            using var form = new PhoneSlotForm(new[] { A, B, c }, null) { Opacity = 0, ShowInTaskbar = false };
            using var bmp = form.Render();
            Assert.True(bmp.Width > 100 && bmp.Height > 100);
        });
    }
}
