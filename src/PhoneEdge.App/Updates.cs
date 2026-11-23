using System.Reflection;
using PhoneEdge.App.Core;
using Velopack;
using Velopack.Sources;

namespace PhoneEdge.App;

/// <summary>앱 버전 표시용.</summary>
internal static class AppInfo
{
    /// <summary>"0.1.0" 형태. 빌드 메타데이터(+커밋)는 뗀다.</summary>
    public static readonly string Version = (Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];
}

/// <summary>
/// Velopack 업데이트 확인·내려받기·적용. 업데이트 소스는 GitHub 정식 릴리스이고,
/// 개발자 테스트용으로 환경 변수 PHONEEDGE_UPDATE_SOURCE 에 폴더를 지정하면 그 폴더를 쓴다.
/// </summary>
internal sealed class UpdateService
{
    public const string RepoUrl = "https://github.com/ssicci/phone-edge";
    public const string SourceEnvVar = "PHONEEDGE_UPDATE_SOURCE";

    private readonly UpdateManager _manager;

    public UpdateService()
    {
        string? folder = Environment.GetEnvironmentVariable(SourceEnvVar);
        IUpdateSource source = string.IsNullOrWhiteSpace(folder)
            ? new GithubSource(RepoUrl, null, false)
            : new SimpleFileSource(new DirectoryInfo(folder));
        _manager = new UpdateManager(source);
    }

    /// <summary>설치본(Velopack)으로 실행 중인지. bin 에서 직접 실행하면 false.</summary>
    public bool IsInstalled => _manager.IsInstalled;

    public Task<UpdateInfo?> CheckAsync() => _manager.CheckForUpdatesAsync();

    public Task DownloadAsync(UpdateInfo info, Action<int> progress) => _manager.DownloadUpdatesAsync(info, progress);

    /// <summary>적용하고 재시작한다. 성공하면 이 프로세스는 끝난다.</summary>
    public void ApplyAndRestart(UpdateInfo info) => _manager.ApplyUpdatesAndRestart(info.TargetFullRelease);
}

/// <summary>새 버전 안내 창. 버튼 동작은 컨트롤러가 처리한다.</summary>
internal sealed class UpdateForm : Form
{
    public enum Choice { Later, Skip, Update }

    private readonly Button _update = new() { Text = "지금 업데이트", AutoSize = true };
    private readonly Button _later = new() { Text = "나중에", AutoSize = true };
    private readonly Button _skip = new() { Text = "이 버전 건너뛰기", AutoSize = true };
    private readonly ProgressBar _bar = new() { Width = 480, Visible = false };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(480, 0), ForeColor = Color.DimGray };

    public UpdateForm(string newVersion, string currentVersion, string notes, Func<UpdateForm, Choice, Task> onChoice)
    {
        Text = "PhoneEdge - 업데이트";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = ShowIcon = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        var title = new Label
        {
            Text = $"새 버전 {newVersion}이 있습니다 (현재 {currentVersion})",
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont!, FontStyle.Bold),
        };
        var caption = new Label { Text = "변경 내용", AutoSize = true, Margin = new Padding(0, 10, 0, 2) };
        var box = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 480, Height = 180, Text = notes };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 10, 0, 0) };
        buttons.Controls.AddRange(new Control[] { _update, _later, _skip });

        var root = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
        root.Controls.AddRange(new Control[] { title, caption, box, _bar, _status, buttons });
        Controls.Add(root);
        CancelButton = _later;

        _update.Click += async (_, _) => await onChoice(this, Choice.Update);
        _later.Click += async (_, _) => await onChoice(this, Choice.Later);
        _skip.Click += async (_, _) => await onChoice(this, Choice.Skip);
    }

    public void SetBusy(bool busy, string status)
    {
        _update.Enabled = _later.Enabled = _skip.Enabled = !busy;
        _bar.Visible = busy;
        _status.Text = status;
    }

    public void SetProgress(int percent)
    {
        _bar.Value = Math.Clamp(percent, 0, 100);
        _status.Text = $"내려받는 중… {percent}%";
    }
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

    public async Task CheckAsync(bool manual)
    {
        if (!_service.IsInstalled)
        {
            if (manual) MessageBox.Show("개발 실행에서는 업데이트를 확인하지 않습니다.", "PhoneEdge", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (manual) MessageBox.Show($"최신 버전입니다 (v{AppInfo.Version}).", "PhoneEdge", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string version = info.TargetFullRelease.Version.ToString();
            if (!UpdatePolicy.ShouldOffer(AppInfo.Version, version, _settings.SkippedVersion, manual))
            {
                Logger.Info($"업데이트 {version}: 표시하지 않음 (건너뛴 버전이거나 현재보다 낮음)");
                if (manual) MessageBox.Show($"최신 버전입니다 (v{AppInfo.Version}).", "PhoneEdge", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Logger.Info($"새 버전 {version} 발견");
            Show(info, version);
        }
        catch (Exception ex)
        {
            Logger.Info("업데이트 확인 실패: " + ex.Message);
            if (manual) MessageBox.Show("업데이트를 확인하지 못했습니다.\n" + ex.Message, "PhoneEdge", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { _checking = false; }
    }

    private void Show(UpdateInfo info, string version)
    {
        string notes = ReleaseNotes.Clean(info.TargetFullRelease.NotesMarkdown);
        _form = new UpdateForm(version, AppInfo.Version, notes, async (form, choice) =>
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
        form.SetBusy(true, "내려받는 중…");
        try
        {
            await _service.DownloadAsync(info, p => { if (!form.IsDisposed) form.BeginInvoke(() => form.SetProgress(p)); });
        }
        catch (Exception ex)
        {
            Logger.Info("업데이트 내려받기 실패: " + ex.Message);
            form.SetBusy(false, "");
            MessageBox.Show(form, "업데이트를 내려받지 못했습니다. 현재 버전으로 계속 동작합니다.\n" + ex.Message, "PhoneEdge", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        form.SetBusy(true, "적용하는 중… 앱이 곧 다시 시작됩니다.");
        _shutdown();   // 폰 모드 해제, 입력 훅 해제, 폰 연결 정리를 끝낸 뒤에 재시작한다
        try
        {
            _service.ApplyAndRestart(info);   // 성공하면 여기서 프로세스가 끝난다
        }
        catch (Exception ex)
        {
            Logger.Info("업데이트 적용 실패: " + ex.Message);
            MessageBox.Show("업데이트를 적용하지 못했습니다. PhoneEdge를 다시 실행해 주세요.\n" + ex.Message, "PhoneEdge", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }
}
