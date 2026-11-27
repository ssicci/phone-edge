using System.Windows.Forms;
using Twinput.Contracts;
using Xunit;

namespace Twinput.App.Tests;

[Collection("UiLanguage")]
public class GuideFormTests
{
    /// <summary>안내 창은 모달이 아닌 Show() 로 열리므로, 닫기 버튼이 직접 창을 닫아야 한다.</summary>
    [Fact]
    public void CloseButtonClosesModelessGuide()
    {
        bool closed = false;
        Exception? error = null;
        var t = new Thread(() =>
        {
            try
            {
                using var form = new GuideForm(new StubLink()) { Opacity = 0, ShowInTaskbar = false };
                form.FormClosed += (_, _) => closed = true;
                form.Show();
                Application.DoEvents();
                var close = Find(form, Twinput.App.Core.L.T("Common_Close")) ?? throw new InvalidOperationException("close button not found");
                close.PerformClick();
                Application.DoEvents();
            }
            catch (Exception ex) { error = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        Assert.Null(error);
        Assert.True(closed);
    }

    /// <summary>문제 안내 중에 폰이 연결되면 "연결됨"으로 바뀌고 저절로 닫혀야 한다 (사용자가 닫을 필요 없음).</summary>
    [Fact]
    public void ClosesByItselfOnceConnected()
    {
        bool closed = false;
        string? title = null;
        Exception? error = null;
        var t = new Thread(() =>
        {
            try
            {
                var form = new GuideForm(new StubLink()) { Opacity = 0, ShowInTaskbar = false, AutoCloseDelay = TimeSpan.FromMilliseconds(200) };
                form.FormClosed += (_, _) => closed = true;
                form.Show();
                form.Apply(PhoneLinkState.Disconnected, new PhoneLinkDiagnosis(PhoneLinkProblem.DebuggingOff, PhoneLinkWarnings.None, null));
                form.Apply(PhoneLinkState.Ready, PhoneLinkDiagnosis.Healthy);
                title = form.TitleText;
                var until = DateTime.UtcNow.AddSeconds(3);
                while (!closed && DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(20); }
                if (!form.IsDisposed) form.Dispose();
            }
            catch (Exception ex) { error = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        Assert.Null(error);
        Assert.Equal(Twinput.App.Core.L.T("Guide_Connected"), title);
        Assert.True(closed);
    }

    /// <summary>연결됐어도 확인할 설정(포인터 가속 경고)이 있으면 사용자가 볼 수 있게 열어 둔다.</summary>
    [Fact]
    public void StaysOpenWhenAWarningNeedsAttention()
    {
        bool closed = false;
        Exception? error = null;
        var t = new Thread(() =>
        {
            try
            {
                using var form = new GuideForm(new StubLink()) { Opacity = 0, ShowInTaskbar = false, AutoCloseDelay = TimeSpan.FromMilliseconds(100) };
                form.FormClosed += (_, _) => closed = true;
                form.Show();
                form.Apply(PhoneLinkState.Ready, new PhoneLinkDiagnosis(PhoneLinkProblem.None, PhoneLinkWarnings.PointerAcceleration, null));
                var until = DateTime.UtcNow.AddMilliseconds(600);
                while (DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(20); }
            }
            catch (Exception ex) { error = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        Assert.Null(error);
        Assert.False(closed);
    }

    private static Button? Find(Control root, string text)
    {
        foreach (Control c in root.Controls)
        {
            if (c is Button b && b.Text == text) return b;
            if (Find(c, text) is { } found) return found;
        }
        return null;
    }
}
