using PhoneEdge.App.Core;

namespace PhoneEdge.App;

/// <summary>트레이 우클릭 메뉴. 앱과 문서용 캡처(--make-docs)가 같은 메뉴를 쓴다.</summary>
internal sealed class TrayMenu
{
    private readonly string _versionText;
    private readonly bool _installed;
    private readonly ToolStripMenuItem _langAuto, _langKo, _langEn;

    public ContextMenuStrip Strip { get; } = new();
    public ToolStripMenuItem Version { get; }
    public ToolStripMenuItem CheckUpdate { get; }
    public ToolStripMenuItem Guide { get; }
    public ToolStripMenuItem Pause { get; }
    public ToolStripMenuItem Language { get; }
    private readonly ToolStripMenuItem _exit;

    /// <param name="onLanguage">언어 하위 메뉴를 고르면 "auto"/"ko"/"en" 으로 호출된다.</param>
    public TrayMenu(string versionText, bool installed, string languageSetting,
        Action onCheckUpdate, Action onGuide, Action onTogglePause, Action<string> onLanguage, Action onExit)
    {
        _versionText = versionText;
        _installed = installed;
        Version = new ToolStripMenuItem { Enabled = false };
        CheckUpdate = new ToolStripMenuItem(null, null, (_, _) => onCheckUpdate()) { Enabled = installed };
        Guide = new ToolStripMenuItem(null, null, (_, _) => onGuide()) { Enabled = false };
        Pause = new ToolStripMenuItem(null, null, (_, _) => onTogglePause());
        _langAuto = new ToolStripMenuItem(null, null, (_, _) => onLanguage(Core.Language.Auto));
        _langKo = new ToolStripMenuItem(null, null, (_, _) => onLanguage(Core.Language.Korean));
        _langEn = new ToolStripMenuItem(null, null, (_, _) => onLanguage(Core.Language.English));
        Language = new ToolStripMenuItem();
        Language.DropDownItems.AddRange(new ToolStripItem[] { _langAuto, _langKo, _langEn });
        _exit = new ToolStripMenuItem(null, null, (_, _) => onExit());

        Strip.Items.Add(Version);
        Strip.Items.Add(CheckUpdate);
        Strip.Items.Add(new ToolStripSeparator());
        Strip.Items.Add(Guide);
        Strip.Items.Add(Pause);
        Strip.Items.Add(Language);
        Strip.Items.Add(new ToolStripSeparator());
        Strip.Items.Add(_exit);
        SetLanguageChecked(languageSetting);
        ApplyLanguage();
    }

    /// <summary>현재 앱 언어로 항목 문구를 다시 채운다 (언어를 바꿀 때 호출).</summary>
    public void ApplyLanguage()
    {
        Version.Text = _versionText + (_installed ? "" : L.T("Menu_DevRun"));
        CheckUpdate.Text = L.T("Menu_CheckUpdate");
        Guide.Text = L.T("Menu_Guide");
        Pause.Text = L.T("Menu_Pause");
        Language.Text = L.T("Menu_Language");
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
