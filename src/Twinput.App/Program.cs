using Twinput.App.Native;

namespace Twinput.App;

internal static class Program
{
    private static TrayApp? _app;

    /// <summary>옛 PhoneEdge 설치에서 옮겨 오는 처리 (설치본에서만 만들어진다). 옛 앱 안내에 쓴다.</summary>
    internal static Core.LegacyMigration? Migration { get; private set; }

    [STAThread]
    private static void Main(string[] args)
    {
        // 설치·제거·업데이트 훅 처리. 뮤텍스보다 먼저, 가장 먼저 실행한다 (시작 메뉴 바로 가기는 Velopack 이 만들고 지운다).
        // 제거할 때는 시작 프로그램 등록(Run, StartupApproved)도 지운다.
        Velopack.VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => { try { new Core.Autostart("").Remove(); } catch { } })
            .Run();
        NativeMethods.AttachConsole(-1);   // 터미널에서 실행하면 로그가 터미널에도 나온다

        // 개발용 명령. 훅·트레이·뮤텍스 없이 파일만 만들고 끝난다.
        if (args.Length == 2 && args[0] == "--make-ico") { TrayIcons.WriteIco(args[1], Core.TrayIconKind.Ready); return; }
        if (args.Length == 2 && args[0] == "--make-docs") { DocImages.Generate(args[1]); return; }

        using var mutex = new Mutex(true, @"Local\Twinput.SingleInstance", out bool first);
        if (!first) return;

        MigrateFromPhoneEdge();   // 설정을 읽기 전에: 옛 설정을 복사해 와야 언어 설정도 이어진다
        Core.L.SetLanguage(new Core.SettingsStore(AppInfo.SettingsPath).Language);   // 시작 실패 메시지도 선택한 언어로

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        // 어떤 경로로 끝나든 훅을 해제하고 폰 모드를 벗어난다.
        Application.ThreadException += (_, e) => { Logger.Info("예외: " + e.Exception); _app?.Shutdown(); Environment.Exit(1); };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { Logger.Info("치명적 예외: " + e.ExceptionObject); _app?.Shutdown(); };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _app?.Shutdown();

        try
        {
            _app = new TrayApp(args);
        }
        catch (Exception ex)
        {
            Logger.Info("시작 실패: " + ex.Message);
            MessageBox.Show(Core.L.F("Start_Failed", ex.Message), "Twinput", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try { Application.Run(_app); }
        finally { _app.Shutdown(); }
    }

    /// <summary>
    /// 이름이 바뀌면서 Velopack 패키지 ID 가 달라져 기존 사용자는 Twinput 을 새로 설치한다.
    /// 설치본이 처음 실행될 때 옛 PhoneEdge 의 설정을 복사하고 옛 자동 시작 등록을 정리한다 (개발 실행에서는 하지 않는다).
    /// </summary>
    private static void MigrateFromPhoneEdge()
    {
        try
        {
            var locator = Velopack.Locators.VelopackLocator.Current;
            if (locator.AppId == null || string.IsNullOrEmpty(locator.RootAppDir)) return;
            var launcher = Path.Combine(locator.RootAppDir, Path.GetFileName(Environment.ProcessPath ?? "Twinput.exe"));
            var migration = new Core.LegacyMigration(Core.LegacyMigration.DefaultOldDir, AppPaths.DataDir);
            var result = migration.Run(new Core.Autostart(launcher));
            Migration = migration;
            Logger.Info($"PhoneEdge 에서 이전: 설정 복사 {result.CopiedSettings}, 옛 자동 시작 제거 {result.RemovedOldRun}, 자동 시작 켬 {result.EnabledAutostart}, 옛 설치 {result.OldInstalled}");
        }
        catch (Exception ex) { Logger.Info("PhoneEdge 에서 이전 실패: " + ex.Message); }
    }
}
