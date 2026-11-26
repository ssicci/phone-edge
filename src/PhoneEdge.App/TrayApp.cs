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
    private readonly SettingsStore _settings;
    private bool _companionPresent, _companionAutoTried;
    private GuideForm? _guide;
    private TrayMenu? _trayMenu;
    private bool _companionBusy;
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

        _updates = new UpdateController(new UpdateService(), settings, () => Shutdown(ExitReason.Update));
        _settings = settings;
        _companionPresent = settings.CompanionPresent;
        var autostart = _updates.LauncherPath is { } launcher ? new Autostart(launcher) : null;
        TrayMenu? trayMenu = null;
        _trayMenu = trayMenu = new TrayMenu($"PhoneEdge v{AppInfo.Version}", _updates.IsInstalled, settings.Language,
            () => OpenFolder(_updates.InstallDir),
            () => autostart?.GetState() ?? AutostartState.Off,
            () => ToggleAutostart(autostart, trayMenu!),
            () => settings.DisableUsbDebuggingOnExit,
            () => { settings.DisableUsbDebuggingOnExit = !settings.DisableUsbDebuggingOnExit; trayMenu!.RefreshAutostart(); },
            () => _link.Companion,
            () => _ = InstallCompanionAsync(),
            () => _ = _updates.CheckAsync(manual: true), () => ShowGuide(), () => SetPaused(!_paused),
            setting =>
            {
                settings.Language = setting;
                trayMenu!.SetLanguageChecked(setting);
                L.SetLanguage(setting);
            },
            ExitApp,
            settings.PhoneSide,
            side =>
            {
                settings.PhoneSide = side;
                _controller.Side = side;   // 폰 모드 중이면 먼저 노트북으로 복귀한다
                trayMenu!.SetPhoneSideChecked(side);
            });
        _controller.Side = settings.PhoneSide;
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
        Microsoft.Win32.SystemEvents.SessionEnding += OnSessionEnding;

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

        _interceptor = new InputInterceptor(_controller,
            (x, y) => _controller.Side == PhoneSide.Left ? screen.LeftEdgeMonitorAt(x, y) : screen.RightEdgeMonitorAt(x, y));
        Logger.Info("입력 훅 설치됨. 폰 모드 비상 복귀: Ctrl+Alt+Esc. 로그: " + Logger.FilePath);

        _ = StartLinkAsync();
    }

    /// <summary>
    /// 폰 도우미 앱을 설치·업데이트한다. 처음 설치할 때는 무엇을 하는지 먼저 확인받는다.
    /// <paramref name="silent"/> 는 이미 설치된 앱의 자동 업데이트로, 확인 창과 결과 창을 띄우지 않는다
    /// (실패하면 메뉴의 "업데이트…" 항목이 그대로 남아 직접 할 수 있다).
    /// </summary>
    private async Task InstallCompanionAsync(bool silent = false)
    {
        if (_companionBusy) return;
        var state = _link.Companion;
        if (!silent && CompanionMenuModel.NeedsConfirmation(state)
            && MessageBox.Show(L.T("Companion_Confirm"), L.T("Companion_ConfirmTitle"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;

        _companionBusy = true;
        _trayMenu?.SetCompanionBusy(true);
        bool ok = false;
        try { ok = await _link.TryFixAsync(PhoneLinkFix.InstallCompanion); }
        catch (Exception ex) { Logger.Info("companion install failed: " + ex.Message); }
        finally
        {
            _companionBusy = false;
            _trayMenu?.SetCompanionBusy(false);
        }
        Logger.Info((silent ? "companion auto-update: " : "companion install: ") + (ok ? "done" : "failed") + ", state " + _link.Companion);
        if (silent) return;
        if (ok) MessageBox.Show(L.T("Companion_Done"), "PhoneEdge", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else MessageBox.Show(L.T("Companion_Failed"), "PhoneEdge", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static void OpenFolder(string dir)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true }); }
        catch (Exception ex) { Logger.Info("open folder failed: " + ex.Message); }
    }

    private static void ToggleAutostart(Autostart? autostart, TrayMenu menu)
    {
        if (autostart == null) return;
        try
        {
            if (autostart.GetState() == AutostartState.On) autostart.Disable(); else autostart.Enable();
            Logger.Info("autostart → " + autostart.GetState());
        }
        catch (Exception ex) { Logger.Info("autostart change failed: " + ex.Message); }
        menu.RefreshAutostart();
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
        foreach (var cs in Enum.GetValues<CompanionState>())
        {
            var state = cs;
            dev.DropDownItems.Add("Companion: " + state, null, (_, _) => fake.SetCompanion(state));
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
        // 폰이 빠지면 엔진은 Unknown 으로 돌리므로, 마지막으로 확인한 값(있음/없음)만 기억한다.
        var companion = _link.Companion;
        if (companion != CompanionState.Unknown)
        {
            bool present = companion is CompanionState.Installed or CompanionState.NeedsUpdate;
            if (present != _companionPresent) { _companionPresent = present; _settings.CompanionPresent = present; }
        }
        _trayMenu?.RefreshCompanion();
        RefreshTray();
        _guide?.Apply(_link.State, _link.Diagnosis);
    }

    private void CheckNotify()
    {
        var d = _link.Diagnosis;
        bool ready = _link.State == PhoneLinkState.Ready;

        // 이미 설치된 도우미 앱은 새 버전이 있으면 묻지 않고 업데이트한다 (연결마다 한 번).
        if (!ready) _companionAutoTried = false;
        if (CompanionMenuModel.ShouldAutoUpdate(_link.Companion, ready, _companionBusy, _controller.Mode == InputMode.Phone, _companionAutoTried))
        {
            _companionAutoTried = true;
            _ = InstallCompanionAsync(silent: true);
        }

        if (_problemShown && (ready || d.Problem is PhoneLinkProblem.None or PhoneLinkProblem.NoDevice))
        {
            _toast?.ClearProblem();   // 해결됐거나 선을 뽑았으면 알림 센터에서도 지운다
            _problemShown = false;
        }
        // 도우미 앱이 있으면 꽂은 직후 USB 디버깅이 켜질 때까지 PC 에는 2~3초 "꺼짐"으로 보인다. 그동안은 알리지 않는다.
        TimeSpan? delay = _companionPresent && d.Problem == PhoneLinkProblem.DebuggingOff ? TimeSpan.FromSeconds(10) : null;
        if (_notifier.Update(d.Problem, ready, DateTime.UtcNow, delay) is { } problem
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
        Shutdown(ExitReason.UserExit);
        ExitThread();
    }

    /// <summary>
    /// 훅 해제와 폰 모드 정리. 정상 종료·예외·프로세스 종료 어디서든 호출되며 여러 번 불려도 안전하다.
    /// </summary>
    public void Shutdown(ExitReason reason = ExitReason.Other)
    {
        if (_shutdown) return;
        _shutdown = true;
        try { Microsoft.Win32.SystemEvents.SessionEnding -= OnSessionEnding; } catch { }
        try { _controller.EmergencyExit(); } catch { }
        try { _interceptor.Dispose(); } catch { }
        DisableUsbDebuggingOnExit(reason);   // 링크를 정리하기 전에 (폰이 아직 연결된 동안) 한다
        try { _notifyTimer.Stop(); _updateTimer.Stop(); _cts.Cancel(); } catch { }
        try { _link.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2)); } catch { }
        try { _tray.Visible = false; _tray.Dispose(); } catch { }
        Logger.Info("종료");
        Logger.Flush();
    }

    /// <summary>Windows 로그오프·종료. 트레이 종료와 같이 처리한다.</summary>
    private void OnSessionEnding(object? sender, Microsoft.Win32.SessionEndingEventArgs e)
    {
        Logger.Info("session ending: " + e.Reason);
        Shutdown(ExitReason.SessionEnding);
    }

    /// <summary>종료할 때 폰의 USB 디버깅을 끈다 (은행·증권 앱이 USB 디버깅이 켜져 있으면 실행을 거부한다). 최대 3초, 실패해도 종료는 계속.</summary>
    private void DisableUsbDebuggingOnExit(ExitReason reason)
        => ExitPolicy.DisableUsbDebuggingIfNeeded(_link, reason, _settings.DisableUsbDebuggingOnExit, _updates.IsInstalled, Logger.Info);

    protected override void Dispose(bool disposing)
    {
        if (disposing) Shutdown();
        base.Dispose(disposing);
    }
}
