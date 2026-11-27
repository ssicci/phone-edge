using System.Reflection;
using Twinput.App.Core;
using Velopack;
using Velopack.Sources;

namespace Twinput.App;

/// <summary>앱 버전 표시용.</summary>
internal static class AppInfo
{
    /// <summary>"0.1.0" 형태. 빌드 메타데이터(+커밋)는 뗀다.</summary>
    public static string SettingsPath => Path.Combine(AppPaths.DataDir, "settings.json");

    public static readonly string Version = (Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];
}

/// <summary>
/// Velopack 업데이트 확인·내려받기·적용. 업데이트 소스는 GitHub 정식 릴리스이고,
/// 개발자 테스트용으로 환경 변수 TWINPUT_UPDATE_SOURCE 에 폴더를 지정하면 그 폴더를 쓴다.
/// </summary>
internal sealed class UpdateService
{
    public const string RepoUrl = "https://github.com/ssicci/twinput";
    public const string SourceEnvVar = "TWINPUT_UPDATE_SOURCE";

    /// <summary>
    /// 업데이트 채널. 옛 PhoneEdge 는 기본 채널(win)의 피드(releases.win.json)를 보는데, Velopack 1.2.161 은 피드의 PackageId 를
    /// 거르지 않으므로 Twinput 은 다른 채널로 배포해 옛 앱이 Twinput 패키지를 받아 자기 위에 적용하지 않게 한다.
    /// release.yml 의 CHANNEL 과 같아야 한다 (설치 파일 이름도 Twinput-win-x64-Setup.exe).
    /// </summary>
    public const string Channel = "win-x64";

    private readonly UpdateManager _manager;

    public UpdateService()
    {
        string? folder = Environment.GetEnvironmentVariable(SourceEnvVar);
        IUpdateSource source = string.IsNullOrWhiteSpace(folder)
            ? new GithubSource(RepoUrl, null, false, new ProxyFallbackDownloader())
            : new SimpleFileSource(new DirectoryInfo(folder));
        _manager = new UpdateManager(source, new UpdateOptions { ExplicitChannel = Channel });
    }

    /// <summary>설치본(Velopack)으로 실행 중인지. bin 에서 직접 실행하면 false.</summary>
    public bool IsInstalled => _manager.IsInstalled;

    /// <summary>설치 루트(설치본) 또는 실행 파일 폴더(개발 실행). 탐색기로 여는 폴더.</summary>
    public string InstallDir => IsInstalled && Velopack.Locators.VelopackLocator.Current.RootAppDir is { Length: > 0 } root ? root : AppContext.BaseDirectory;

    /// <summary>설치 루트의 Velopack 실행기. 업데이트로 current 폴더가 바뀌어도 이 경로는 그대로다. 설치본이 아니면 null.</summary>
    public string? LauncherPath => IsInstalled && Velopack.Locators.VelopackLocator.Current.RootAppDir is { Length: > 0 } root
        ? Path.Combine(root, Path.GetFileName(Environment.ProcessPath ?? "Twinput.exe"))
        : null;

    public Task<UpdateInfo?> CheckAsync() => _manager.CheckForUpdatesAsync();

    public Task DownloadAsync(UpdateInfo info, Action<int> progress) => _manager.DownloadUpdatesAsync(info, progress);

    /// <summary>적용하고 재시작한다. 성공하면 이 프로세스는 끝난다.</summary>
    public void ApplyAndRestart(UpdateInfo info) => _manager.ApplyUpdatesAndRestart(info.TargetFullRelease);
}

/// <summary>새 버전 안내 창. 버튼 동작은 컨트롤러가 처리한다. 언어가 바뀌면 ApplyLanguage 로 문구를 다시 채운다.</summary>
internal sealed class UpdateForm : Form
{
    public enum Choice { Later, Skip, Update }

    private readonly string _newVersion, _currentVersion, _rawNotes;
    private readonly Button _update = new() { AutoSize = true };
    private readonly Button _later = new() { AutoSize = true };
    private readonly Button _skip = new() { AutoSize = true };
    private readonly ProgressBar _bar = new() { Width = 480, Visible = false };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(480, 0), ForeColor = Color.DimGray };
    private readonly Label _title = new() { AutoSize = true, MaximumSize = new Size(480, 0), Font = new Font(SystemFonts.MessageBoxFont!, FontStyle.Bold) };
    private readonly Label _caption = new() { AutoSize = true, Margin = new Padding(0, 10, 0, 2) };
    private readonly TextBox _box = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 480, Height = 260 };
    private string _statusKey = "";
    private int _percent;

    /// <param name="rawNotes">릴리스 노트 마크다운 원문. 현재 언어 부분만 골라 보여 준다.</param>
    public UpdateForm(string newVersion, string currentVersion, string? rawNotes, Func<UpdateForm, Choice, Task> onChoice)
    {
        _newVersion = newVersion;
        _currentVersion = currentVersion;
        _rawNotes = rawNotes ?? "";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = ShowIcon = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 10, 0, 0) };
        buttons.Controls.AddRange(new Control[] { _update, _later, _skip });

        var root = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
        root.Controls.AddRange(new Control[] { _title, _caption, _box, _bar, _status, buttons });
        Controls.Add(root);
        CancelButton = _later;
        ActiveControl = _update;   // 변경 내용 상자가 전체 선택된 채로 열리지 않게 한다

        _update.Click += async (_, _) => await onChoice(this, Choice.Update);
        _later.Click += async (_, _) => await onChoice(this, Choice.Later);
        _skip.Click += async (_, _) => await onChoice(this, Choice.Skip);
        ApplyLanguage();
    }

    public void ApplyLanguage()
    {
        Text = L.T("Update_Title");
        _title.Text = L.F("Update_Header", _newVersion, _currentVersion);
        _caption.Text = L.T("Update_Notes");
        _box.Text = ReleaseNotes.Clean(ReleaseNotes.ForLanguage(_rawNotes, L.Lang));
        _update.Text = L.T("Update_Now");
        _later.Text = L.T("Update_Later");
        _skip.Text = L.T("Update_Skip");
        ShowStatus();
        _box.SelectionStart = 0;
        _box.SelectionLength = 0;
    }

    /// <param name="statusKey">리소스 키 (없으면 빈 문자열).</param>
    public void SetBusy(bool busy, string statusKey)
    {
        _update.Enabled = _later.Enabled = _skip.Enabled = !busy;
        _bar.Visible = busy;
        _statusKey = statusKey;
        ShowStatus();
    }

    public void SetProgress(int percent)
    {
        _bar.Value = Math.Clamp(percent, 0, 100);
        _percent = percent;
        _statusKey = "Update_Progress";
        ShowStatus();
    }

    private void ShowStatus() => _status.Text = _statusKey.Length == 0 ? "" : _statusKey == "Update_Progress" ? L.F(_statusKey, _percent) : L.T(_statusKey);
}

/// <summary>업데이트 확인 흐름: 확인 → (건너뛴 버전 판단) → 창 → 내려받기 → 정리 후 적용·재시작.</summary>
internal sealed class UpdateController
{
    private readonly UpdateService _service;
    private readonly SettingsStore _settings;
    private readonly Action _shutdown;
    private UpdateForm? _form;
    private bool _checking;

    public UpdateController(UpdateService service, SettingsStore settings, Action shutdown)
    {
        _service = service;
        _settings = settings;
        _shutdown = shutdown;
    }

    public bool IsInstalled => _service.IsInstalled;
    public string InstallDir => _service.InstallDir;
    public string? LauncherPath => _service.LauncherPath;

    /// <summary>열려 있는 업데이트 창의 언어를 바꾼다.</summary>
    public void ApplyLanguage() { if (_form is { IsDisposed: false }) _form.ApplyLanguage(); }

    public async Task CheckAsync(bool manual)
    {
        if (!_service.IsInstalled)
        {
            if (manual) MessageBox.Show(L.T("Update_DevRun"), "Twinput", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_checking || _form is { IsDisposed: false })
        {
            _form?.Activate();
            return;
        }
        _checking = true;
        try
        {
            var info = await _service.CheckAsync();
            if (info == null)
            {
                Logger.Info("업데이트 없음");
                if (manual) MessageBox.Show(L.F("Update_UpToDate", AppInfo.Version), "Twinput", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string version = info.TargetFullRelease.Version.ToString();
            if (!UpdatePolicy.ShouldOffer(AppInfo.Version, version, _settings.SkippedVersion, manual))
            {
                Logger.Info($"업데이트 {version}: 표시하지 않음 (건너뛴 버전이거나 현재보다 낮음)");
                if (manual) MessageBox.Show(L.F("Update_UpToDate", AppInfo.Version), "Twinput", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Logger.Info($"새 버전 {version} 발견");
            Show(info, version);
        }
        catch (Exception ex)
        {
            Logger.Info("업데이트 확인 실패: " + ex.Message);
            if (manual) MessageBox.Show(L.F("Update_CheckFailed", ex.Message), "Twinput", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { _checking = false; }
    }

    private void Show(UpdateInfo info, string version)
    {
                _form = new UpdateForm(version, AppInfo.Version, info.TargetFullRelease.NotesMarkdown, async (form, choice) =>
        {
            switch (choice)
            {
                case UpdateForm.Choice.Later:
                    form.Close();
                    break;
                case UpdateForm.Choice.Skip:
                    _settings.SkippedVersion = version;
                    Logger.Info($"버전 {version} 건너뛰기");
                    form.Close();
                    break;
                default:
                    await ApplyAsync(form, info);
                    break;
            }
        });
        _form.Show();
        _form.Activate();
    }

    private async Task ApplyAsync(UpdateForm form, UpdateInfo info)
    {
        form.SetBusy(true, "Update_Downloading");
        try
        {
            await _service.DownloadAsync(info, p => { if (!form.IsDisposed) form.BeginInvoke(() => form.SetProgress(p)); });
        }
        catch (Exception ex)
        {
            Logger.Info("업데이트 내려받기 실패: " + ex.Message);
            form.SetBusy(false, "");
            MessageBox.Show(form, L.F("Update_DownloadFailed", ex.Message), "Twinput", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        form.SetBusy(true, "Update_Applying");
        _shutdown();   // 폰 모드 해제, 입력 훅 해제, 폰 연결 정리를 끝낸 뒤에 재시작한다
        try
        {
            _service.ApplyAndRestart(info);   // 성공하면 여기서 프로세스가 끝난다
        }
        catch (Exception ex)
        {
            Logger.Info("업데이트 적용 실패: " + ex.Message);
            MessageBox.Show(L.F("Update_ApplyFailed", ex.Message), "Twinput", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }
}
