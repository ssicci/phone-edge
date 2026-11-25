using System.Windows.Forms;
using PhoneEdge.App.Core;
using PhoneEdge.Contracts;
using Xunit;

namespace PhoneEdge.App.Tests;

/// <summary>앱 언어(전역 L)를 바꾸는 테스트는 같은 컬렉션에서 하나씩 실행한다.</summary>
[CollectionDefinition("UiLanguage", DisableParallelization = true)]
public class UiLanguageCollection { }

[Collection("UiLanguage")]
public class LiveLanguageTests
{
    private static void RunSta(Action body)
    {
        Exception? error = null;
        var t = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { error = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        Assert.Null(error);
    }

    [Fact]
    public void TrayMenuTextChangesWithTheLanguage()
    {
        RunSta(() =>
        {
            string? chosen = null;
            L.SetLanguage("en");
            var menu = new TrayMenu("PhoneEdge v1.2.3", installed: false, Language.Auto, () => { }, () => AutostartState.Off, () => { }, () => { }, () => { }, () => { }, s => chosen = s, () => { });
            Assert.Equal("PhoneEdge v1.2.3 (development run)", menu.Version.Text);
            Assert.Equal("Pause", menu.Pause.Text);
            Assert.True(menu.Language.DropDownItems[0].Text == "Auto");

            L.SetLanguage("ko");
            menu.ApplyLanguage();
            Assert.Equal("PhoneEdge v1.2.3 (개발 실행)", menu.Version.Text);
            Assert.Equal("일시 정지", menu.Pause.Text);
            Assert.Equal("연결 문제 해결…", menu.Guide.Text);
            Assert.Equal("자동", menu.Language.DropDownItems[0].Text);
            Assert.Equal("언어 / Language", menu.Language.Text);   // 어느 언어에서도 찾을 수 있게 병기

            ((ToolStripMenuItem)menu.Language.DropDownItems[2]).PerformClick();
            Assert.Equal(Language.English, chosen);
            menu.SetLanguageChecked("en");
            Assert.True(((ToolStripMenuItem)menu.Language.DropDownItems[2]).Checked);
            Assert.False(((ToolStripMenuItem)menu.Language.DropDownItems[0]).Checked);
            L.SetLanguage("en");
        });
    }

    [Fact]
    public void OpenWindowsSwitchLanguageInPlace()
    {
        RunSta(() =>
        {
            L.SetLanguage("en");
            var fake = new FakePhoneLink(_ => { });
            fake.SetDiagnosis(PhoneLinkProblem.Unauthorized, PhoneLinkWarnings.PointerAcceleration);
            using var guide = new GuideForm(fake) { Opacity = 0, ShowInTaskbar = false };
            using var update = new UpdateForm("0.2.0", "0.1.0", "English notes\n\n---\n\n한글 노트", (_, _) => Task.CompletedTask) { Opacity = 0, ShowInTaskbar = false };
            guide.Show();
            update.Show();
            Assert.Equal("PhoneEdge - Fix connection problem", guide.Text);
            Assert.Equal("PhoneEdge - Update", update.Text);
            Assert.Equal("English notes", Box(update).Text.Trim());

            L.SetLanguage("ko");
            guide.ApplyLanguage();
            update.ApplyLanguage();
            Assert.Equal("PhoneEdge - 연결 문제 해결", guide.Text);
            Assert.Equal("PhoneEdge - 업데이트", update.Text);
            Assert.Equal("한글 노트", Box(update).Text.Trim());
            Assert.NotNull(FindButton(guide, "다시 시도"));
            Assert.NotNull(FindButton(update, "지금 업데이트"));
            L.SetLanguage("en");
        });
    }

    private static TextBox Box(Control root) => (TextBox)Find(root, c => c is TextBox { Multiline: true })!;
    private static Button? FindButton(Control root, string text) => (Button?)Find(root, c => c is Button b && b.Text == text);

    private static Control? Find(Control root, Func<Control, bool> match)
    {
        foreach (Control c in root.Controls)
        {
            if (match(c)) return c;
            if (Find(c, match) is { } f) return f;
        }
        return null;
    }
}
