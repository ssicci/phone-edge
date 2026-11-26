using PhoneEdge.App.Core;
using PhoneEdge.Contracts;

namespace PhoneEdge.App;

/// <summary>트레이 우클릭 메뉴. 앱과 문서용 캡처(--make-docs)가 같은 메뉴를 쓴다.</summary>
internal sealed class TrayMenu
{
    private readonly string _versionText;
    private readonly bool _installed;
    private readonly ToolStripMenuItem _langAuto, _langKo, _langEn;
    public ToolStripMenuItem PhoneSlotItem { get; }

    public ContextMenuStrip Strip { get; } = new();
    public ToolStripMenuItem Version { get; }
    public ToolStripMenuItem CheckUpdate { get; }
    public ToolStripMenuItem Guide { get; }
    public ToolStripMenuItem Pause { get; }
    public ToolStripMenuItem Language { get; }
    public ToolStripMenuItem OpenFolder { get; }
    public ToolStripMenuItem AutostartItem { get; }
    public ToolStripMenuItem DisableDebuggingItem { get; }
    public ToolStripMenuItem CompanionItem { get; }
    private readonly Func<CompanionState> _companionState;
    private bool _companionBusy;
    private readonly Func<AutostartState> _autostartState;
    private readonly Func<bool> _disableDebuggingOnExit;
    private readonly ToolStripMenuItem _exit;

    /// <param name="onLanguage">언어 하위 메뉴를 고르면 "auto"/"ko"/"en" 으로 호출된다.</param>
    /// <param name="autostartState">메뉴를 열 때마다 호출해 자동 실행 체크 상태를 다시 읽는다.</param>
    public TrayMenu(string versionText, bool installed, string languageSetting,
        Action onOpenFolder, Func<AutostartState> autostartState, Action onToggleAutostart,
        Func<bool> disableDebuggingOnExit, Action onToggleDisableDebugging,
        Func<CompanionState> companionState, Action onCompanion,
        Action onCheckUpdate, Action onGuide, Action onTogglePause, Action<string> onLanguage, Action onExit,
        Action? onPhoneSlot = null)
    {
        _autostartState = autostartState;
        _versionText = versionText;
        _installed = installed;
        Version = new ToolStripMenuItem { Enabled = false };
        _disableDebuggingOnExit = disableDebuggingOnExit;
        _companionState = companionState;
        CompanionItem = new ToolStripMenuItem(null, null, (_, _) => onCompanion());
        DisableDebuggingItem = new ToolStripMenuItem(null, null, (_, _) => onToggleDisableDebugging());
        OpenFolder = new ToolStripMenuItem(null, null, (_, _) => onOpenFolder());
        AutostartItem = new ToolStripMenuItem(null, null, (_, _) => onToggleAutostart()) { Enabled = installed };
        CheckUpdate = new ToolStripMenuItem(null, null, (_, _) => onCheckUpdate()) { Enabled = installed };
        Guide = new ToolStripMenuItem(null, null, (_, _) => onGuide()) { Enabled = false };
        Pause = new ToolStripMenuItem(null, null, (_, _) => onTogglePause());
        _langAuto = new ToolStripMenuItem(null, null, (_, _) => onLanguage(Core.Language.Auto));
        _langKo = new ToolStripMenuItem(null, null, (_, _) => onLanguage(Core.Language.Korean));
        _langEn = new ToolStripMenuItem(null, null, (_, _) => onLanguage(Core.Language.English));
        Language = new ToolStripMenuItem();
        Language.DropDownItems.AddRange(new ToolStripItem[] { _langAuto, _langKo, _langEn });
        _exit = new ToolStripMenuItem(null, null, (_, _) => onExit());
        PhoneSlotItem = new ToolStripMenuItem(null, null, (_, _) => onPhoneSlot?.Invoke());

        Strip.Items.Add(Version);
        Strip.Items.Add(OpenFolder);
        Strip.Items.Add(CheckUpdate);
        Strip.Items.Add(new ToolStripSeparator());
        Strip.Items.Add(Guide);
        Strip.Items.Add(Pause);
        Strip.Items.Add(PhoneSlotItem);
        Strip.Items.Add(AutostartItem);
        Strip.Items.Add(DisableDebuggingItem);
        Strip.Items.Add(CompanionItem);
        Strip.Items.Add(Language);
        Strip.Items.Add(new ToolStripSeparator());
        Strip.Items.Add(_exit);
        SetLanguageChecked(languageSetting);
        ApplyLanguage();
        Strip.Opening += (_, _) => RefreshAutostart();   // 사용자가 Windows 설정에서 바꾼 것을 반영한다
        RefreshAutostart();
    }

    /// <summary>레지스트리에서 자동 실행 상태를 다시 읽어 체크 표시와 도움말에 반영한다.</summary>
    public void RefreshAutostart()
    {
        RefreshCompanion();
        var state = _installed ? _autostartState() : AutostartState.Off;
        AutostartItem.Checked = state == AutostartState.On;
        DisableDebuggingItem.Checked = _disableDebuggingOnExit();
        AutostartItem.ToolTipText = state == AutostartState.DisabledInWindows ? L.T("Menu_AutostartDisabledHint") : "";
    }

    /// <summary>폰 도우미 앱 상태(또는 진행 중 여부)가 바뀌면 항목을 다시 그린다.</summary>
    public void RefreshCompanion()
    {
        var m = CompanionMenuModel.For(_companionState(), _companionBusy);
        CompanionItem.Visible = m.Visible;
        CompanionItem.Enabled = m.Enabled;
        CompanionItem.Checked = m.Checked;
        CompanionItem.Text = L.T(m.TextKey);
    }

    public void SetCompanionBusy(bool busy)
    {
        _companionBusy = busy;
        RefreshCompanion();
    }

    /// <summary>현재 앱 언어로 항목 문구를 다시 채운다 (언어를 바꿀 때 호출).</summary>
    public void ApplyLanguage()
    {
        Version.Text = _versionText + (_installed ? "" : L.T("Menu_DevRun"));
        OpenFolder.Text = L.T("Menu_OpenFolder");
        AutostartItem.Text = L.T("Menu_Autostart") + (_installed ? "" : L.T("Menu_AutostartDevNote"));
        DisableDebuggingItem.Text = L.T("Menu_DisableDebugging");
        DisableDebuggingItem.ToolTipText = L.T("Menu_DisableDebuggingHint");
        RefreshCompanion();
        CheckUpdate.Text = L.T("Menu_CheckUpdate");
        Guide.Text = L.T("Menu_Guide");
        Pause.Text = L.T("Menu_Pause");
        Language.Text = L.T("Menu_Language");
        PhoneSlotItem.Text = L.T("Menu_PhoneSlot");
        _langAuto.Text = L.T("Menu_LangAuto");
        _langKo.Text = L.T("Menu_LangKo");
        _langEn.Text = L.T("Menu_LangEn");
        _exit.Text = L.T("Menu_Exit");
    }

    public void SetLanguageChecked(string setting)
    {
        string s = Core.Language.Normalize(setting);
        _langAuto.Checked = s == Core.Language.Auto;
        _langKo.Checked = s == Core.Language.Korean;
        _langEn.Checked = s == Core.Language.English;
    }

    /// <summary>개발용 항목(가짜 폰)은 종료 구분선 앞에 끼운다.</summary>
    public void InsertBeforeExit(ToolStripItem item) => Strip.Items.Insert(Strip.Items.Count - 2, item);
}
