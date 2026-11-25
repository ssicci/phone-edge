using PhoneEdge.App.Native;

namespace PhoneEdge.App;

internal static class Program
{
    private static TrayApp? _app;

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

        Core.L.SetLanguage(new Core.SettingsStore(AppInfo.SettingsPath).Language);   // 시작 실패 메시지도 선택한 언어로
        using var mutex = new Mutex(true, @"Local\PhoneEdge.SingleInstance", out bool first);
        if (!first) return;

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
            MessageBox.Show(Core.L.F("Start_Failed", ex.Message), "PhoneEdge", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try { Application.Run(_app); }
        finally { _app.Shutdown(); }
    }
}
