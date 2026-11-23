namespace PhoneEdge.App;

/// <summary>트레이 우클릭 메뉴. 앱과 문서용 캡처(--make-docs)가 같은 메뉴를 쓴다.</summary>
internal sealed class TrayMenu
{
    public ContextMenuStrip Strip { get; } = new();
    public ToolStripMenuItem Version { get; }
    public ToolStripMenuItem CheckUpdate { get; }
    public ToolStripMenuItem Guide { get; }
    public ToolStripMenuItem Pause { get; }

    public TrayMenu(string versionText, bool installed, Action onCheckUpdate, Action onGuide, Action onTogglePause, Action onExit)
    {
        Version = new ToolStripMenuItem(versionText + (installed ? "" : " (개발 실행)")) { Enabled = false };
        CheckUpdate = new ToolStripMenuItem("업데이트 확인", null, (_, _) => onCheckUpdate()) { Enabled = installed };
        Guide = new ToolStripMenuItem("연결 문제 해결…", null, (_, _) => onGuide()) { Enabled = false };
        Pause = new ToolStripMenuItem("일시 정지", null, (_, _) => onTogglePause());
        Strip.Items.Add(Version);
        Strip.Items.Add(CheckUpdate);
        Strip.Items.Add(new ToolStripSeparator());
        Strip.Items.Add(Guide);
        Strip.Items.Add(Pause);
        Strip.Items.Add(new ToolStripSeparator());
        Strip.Items.Add(new ToolStripMenuItem("종료", null, (_, _) => onExit()));
    }

    /// <summary>개발용 항목(가짜 폰)은 종료 구분선 앞에 끼운다.</summary>
    public void InsertBeforeExit(ToolStripItem item) => Strip.Items.Insert(Strip.Items.Count - 2, item);
}
