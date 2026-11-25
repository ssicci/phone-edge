using PhoneEdge.Contracts;

namespace PhoneEdge.App.Core;

/// <summary>폰 도우미 앱 메뉴 항목을 어떻게 보여 줄지. 순수 로직.</summary>
/// <param name="Visible">Unknown 이면 숨긴다.</param>
/// <param name="Enabled">설치됨이거나 진행 중이면 누를 수 없다.</param>
/// <param name="Checked">설치됨이면 체크 표시.</param>
/// <param name="TextKey">리소스 키.</param>
public sealed record CompanionMenuModel(bool Visible, bool Enabled, bool Checked, string TextKey)
{
    public static CompanionMenuModel For(CompanionState state, bool busy)
    {
        if (state == CompanionState.Unknown) return new(false, false, false, "Menu_CompanionInstall");
        if (busy) return new(true, false, false, "Menu_CompanionBusy");
        return state switch
        {
            CompanionState.NotInstalled => new(true, true, false, "Menu_CompanionInstall"),
            CompanionState.NeedsUpdate => new(true, true, false, "Menu_CompanionUpdate"),
            _ => new(true, false, true, "Menu_CompanionInstalled"),
        };
    }

    /// <summary>처음 설치할 때만 무엇을 하는지 확인 창을 띄운다 (업데이트는 이미 동의한 앱의 갱신이다).</summary>
    public static bool NeedsConfirmation(CompanionState state) => state == CompanionState.NotInstalled;
}
