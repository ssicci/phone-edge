using PhoneEdge.App.Core;
using PhoneEdge.App.Native;
using PhoneEdge.Contracts;

namespace PhoneEdge.App;

/// <summary>트레이 UI와 조립. 입력 훅·상태 기계·IPhoneLink 를 묶고, 종료 시 정리를 보장한다.</summary>
internal sealed class TrayApp : ApplicationContext
{
    private readonly Control _ui = new();
    private readonly NotifyIcon _tray = new();
    private readonly IPhoneLink _link;
    private readonly ModeController _controller;
    private readonly InputInterceptor _interceptor;
    private readonly CancellationTokenSource _cts = new();
    private readonly ToolStripMenuItem _pauseItem, _guideItem;
    private readonly ProblemNotifier _notifier = new(TimeSpan.FromSeconds(2));
    private readonly System.Windows.Forms.Timer _notifyTimer = new() { Interval = 500 };
    private readonly WindowsToast? _toast = WindowsToast.TryCreate();
    private readonly UpdateController _updates;
    private readonly ToolStripMenuItem _checkUpdateItem;
    private readonly System.Windows.Forms.Timer _updateTimer = new() { Interval = 10_000 };
    private GuideForm? _guide;
    private bool _paused, _shutdown, _problemShown;

    public TrayApp(string[] args)
    {
        _ = _ui.Handle;   // UI 스레드로 옮길 수 있게 핸들을 미리 만든다
        var settings = new SettingsStore(AppInfo.SettingsPath);
        L.SetLanguage(settings.Language);

        _link = PhoneLinkFactory.Create(args, Logger.Info);   // 조립 지점은 PhoneLinkFactory 한 곳
        var screen = new Win32Screen();
        _controller = new ModeController(_link, screen);
        _controller.ModeChanged += mode => { Logger.Info($"모드 → {mode}"); RefreshTray(); };
        _link.StateChanged += s => PostUi(() => { Logger.Info($"링크 상태 → {s}"); _controller.OnLinkStateChanged(s); OnLinkChanged(); });
        _link.ExitRequested += y => PostUi(() => _controller.OnExitRequested(y));
        _link.DiagnosisChanged += d => PostUi(() => { Logger.Info($"진단 → {d.Problem} / {d.Warnings}"); OnLinkChanged(); });

        _updates = new UpdateController(new UpdateService(), settings, Shutdown);
        TrayMenu? trayMenu = null;
        trayMenu = new TrayMenu($"PhoneEdge v{AppInfo.Version}", _updates.IsInstalled, settings.Language,
            () => _ = _updates.CheckAsync(manual: true), () => ShowGuide(), () => SetPaused(!_paused),
            setting =>
            {
                settings.Language = setting;
                trayMenu!.SetLanguageChecked(setting);
                L.SetLanguage(setting);
            },
            ExitApp);
        // 언어가 바뀌면 메뉴, 툴팁, 열려 있는 창이 바로 바뀐다.
        L.Changed += () =>
        {
            Logger.Info("언어 → " + L.Lang);
            trayMenu!.ApplyLanguage();
            RefreshTray();
            _guide?.ApplyLanguage();
            _updates.ApplyLanguage();
        };
        _pauseItem = trayMenu.Pause;
        _guideItem = trayMenu.Guide;
        _checkUpdateItem = trayMenu.CheckUpdate;
        if (_link is FakePhoneLink fake) AddFakeMenu(trayMenu, fake);
        _tray.ContextMenuStrip = trayMenu.Strip;
        if (_toast != null)
            _toast.Activated += arg => PostUi(() =>
            {
                Logger.Info("알림 클릭: " + arg);
                if (arg == ToastXml.Retry) _ = _link.TryFixAsync(PhoneLinkFix.Retry);
                else ShowGuide(bringToFront: true);
            });
        _tray.MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left && TrayStatus.HasIssue(_link.Diagnosis)) ShowGuide(); };
        _tray.Visible = true;
        RefreshTray();

        _notifyTimer.Tick += (_, _) => CheckNotify();
        _notifyTimer.Start();

        // 시작 10초 뒤, 이후 6시간마다 확인한다. 폰 모드 중에는 창을 띄우지 않고 5분 뒤로 미룬다.
        _updateTimer.Tick += (_, _) =>
        {
            if (_controller.Mode == InputMode.Phone) { _updateTimer.Interval = 5 * 60 * 1000; return; }
            _updateTimer.Interval = 6 * 60 * 60 * 1000;
            _ = _updates.CheckAsync(manual: false);
        };
        if (_updates.IsInstalled) _updateTimer.Start();

        _interceptor = new InputInterceptor(_controller, screen.RightEdgeMonitorAt);
        Logger.Info("입력 훅 설치됨. 폰 모드 비상 복귀: Ctrl+Alt+Esc. 로그: " + Logger.FilePath);

        _ = StartLinkAsync();
    }

    private static void AddFakeMenu(TrayMenu menu, FakePhoneLink fake)
    {
        var dev = new ToolStripMenuItem("(dev) Simulate problem");
        dev.DropDownItems.Add("Healthy", null, (_, _) => fake.SetDiagnosis(PhoneLinkProblem.None, fake.Diagnosis.Warnings));
        foreach (var p in Enum.GetValues<PhoneLinkProblem>().Where(p => p != PhoneLinkProblem.None))
        {
            var problem = p;
            dev.DropDownItems.Add(problem.ToString(), null, (_, _) => fake.SetDiagnosis(problem, fake.Diagnosis.Warnings));
        }
        dev.DropDownItems.Add(new ToolStripSeparator());
        dev.DropDownItems.Add("Warning: pointer acceleration on", null, (_, _) =>
            fake.SetDiagnosis(fake.Diagnosis.Problem, fake.Diagnosis.Warnings | PhoneLinkWarnings.PointerAcceleration));
        dev.DropDownItems.Add("No warnings", null, (_, _) => fake.SetDiagnosis(fake.Diagnosis.Problem, PhoneLinkWarnings.None));
        dev.DropDownItems.Add(new ToolStripSeparator());
        dev.DropDownItems.Add("Disconnect (no diagnosis)", null, (_, _) => fake.SetConnected(false));
        menu.InsertBeforeExit(dev);
    }

    private async Task StartLinkAsync()
    {
        try { await _link.StartAsync(_cts.Token); }
        catch (Exception ex) { Logger.Info("링크 시작 실패: " + ex.Message); }
    }

    private void PostUi(Action action)
    {
        try
        {
            if (!_ui.IsDisposed && _ui.IsHandleCreated) _ui.BeginInvoke(action);
        }
        catch (ObjectDisposedException) { }
    }

    private void OnLinkChanged()
    {
        RefreshTray();
        _guide?.Apply(_link.State, _link.Diagnosis);
    }

    private void CheckNotify()
    {
        var d = _link.Diagnosis;
        bool ready = _link.State == PhoneLinkState.Ready;
        if (_problemShown && (ready || d.Problem is PhoneLinkProblem.None or PhoneLinkProblem.NoDevice))
        {
            _toast?.ClearProblem();   // 해결됐거나 선을 뽑았으면 알림 센터에서도 지운다
            _problemShown = false;
        }
        if (_notifier.Update(d.Problem, ready, DateTime.UtcNow) is { } problem
            && GuidanceTable.For(problem) is { } g)
        {
            _problemShown = true;
            if (_toast?.ShowProblem(g) == true)
            {
                Logger.Info($"Windows 알림 표시: {problem}");
            }
            else
            {
                ShowGuide(bringToFront: true);   // Windows 알림을 쓸 수 없으면 안내 창을 직접 띄운다
                Logger.Info($"안내 창 표시: {problem}");
            }
        }
    }

    private void ShowGuide() => ShowGuide(bringToFront: false);

    private void ShowGuide(bool bringToFront)
    {
        if (_guide == null || _guide.IsDisposed)
        {
            _guide = new GuideForm(_link);
            _guide.FormClosed += (_, _) => _guide = null;
            _guide.Show();
        }
        _guide.Apply(_link.State, _link.Diagnosis);
        if (bringToFront)
        {
            // 백그라운드 앱은 포커스를 뺏을 수 없으므로 잠깐 TopMost 로 맨 위에 올린 뒤 원래대로 둔다.
            if (_guide.WindowState == FormWindowState.Minimized) _guide.WindowState = FormWindowState.Normal;
            _guide.TopMost = true;
            var g = _guide;
            g.BeginInvoke(() => { if (!g.IsDisposed) g.TopMost = false; });
        }
        _guide.Activate();
    }

    private void SetPaused(bool paused)
    {
        _paused = paused;
        _pauseItem.Checked = paused;
        _controller.Enabled = !paused;
        Logger.Info(paused ? "paused" : "resumed");
        RefreshTray();
    }

    private void RefreshTray()
    {
        var d = _link.Diagnosis;
        var kind = TrayStatus.SelectIcon(_paused, _controller.Mode, _link.State, d);
        _tray.Icon = TrayIcons.Get(kind, TrayIcons.TraySize);
        _tray.Text = TrayStatus.Tooltip(kind, d);
        _guideItem.Enabled = TrayStatus.HasIssue(d);
    }

    private void ExitApp()
    {
        Shutdown();
        ExitThread();
    }

    /// <summary>
    /// 훅 해제와 폰 모드 정리. 정상 종료·예외·프로세스 종료 어디서든 호출되며 여러 번 불려도 안전하다.
    /// </summary>
    public void Shutdown()
    {
        if (_shutdown) return;
        _shutdown = true;
        try { _controller.EmergencyExit(); } catch { }
        try { _interceptor.Dispose(); } catch { }
        try { _notifyTimer.Stop(); _updateTimer.Stop(); _cts.Cancel(); } catch { }
        try { _link.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2)); } catch { }
        try { _tray.Visible = false; _tray.Dispose(); } catch { }
        Logger.Info("종료");
        Logger.Flush();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Shutdown();
        base.Dispose(disposing);
    }
}
