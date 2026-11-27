using System.Drawing;
using System.Windows.Forms;
using Twinput.App.Core;
using Twinput.Contracts;
using Xunit;

namespace Twinput.App.Tests;

/// <summary>
/// v0.5.0: 엔진이 연결 뒤 도우미 앱 상태를 Unknown → NeedsUpdate 로 바꿨는데 트레이 메뉴에 "업데이트" 항목이 나오지 않았다.
/// 실제 TrayMenu 객체로 상태가 시작 뒤에 바뀌는 경우를 재현한다.
/// </summary>
[Collection("UiLanguage")]
public class CompanionMenuLiveTests
{
    [Fact]
    public void ItemAppearsWhenStateChangesAfterStartup()
    {
        Exception? error = null;
        bool visible = false;
        string? text = null;
        var t = new Thread(() =>
        {
            try
            {
                L.SetLanguage("ko");
                var state = CompanionState.Unknown;
                var menu = new TrayMenu("Twinput v0.5.0", installed: true, Language.Auto, () => { }, () => AutostartState.Off, () => { },
                    () => true, () => { }, () => state, () => { }, () => { }, () => { }, () => { }, _ => { }, () => { });

                state = CompanionState.NeedsUpdate;   // 연결되어 엔진이 상태를 정함
                menu.RefreshCompanion();              // DiagnosisChanged → OnLinkChanged 가 하는 일

                // 사용자가 트레이를 우클릭해 메뉴를 연다
                menu.Strip.Show(new Point(-2000, -2000));
                Application.DoEvents();
                visible = menu.CompanionItem.Visible;
                text = menu.CompanionItem.Text;
                menu.Strip.Close();
            }
            catch (Exception ex) { error = ex; }
            finally { L.SetLanguage("auto"); }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        Assert.Null(error);
        Assert.Equal("폰 도우미 앱 업데이트…", text);
        Assert.True(visible);
    }
}
