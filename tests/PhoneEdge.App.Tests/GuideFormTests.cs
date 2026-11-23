using System.Windows.Forms;
using Xunit;

namespace PhoneEdge.App.Tests;

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
                var close = Find(form, PhoneEdge.App.Core.L.T("Common_Close")) ?? throw new InvalidOperationException("close button not found");
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
