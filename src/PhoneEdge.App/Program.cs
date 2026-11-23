using PhoneEdge.App.Native;

namespace PhoneEdge.App;

internal static class Program
{
    private static TrayApp? _app;

    [STAThread]
    private static void Main(string[] args)
    {
        NativeMethods.AttachConsole(-1);   // 터미널에서 실행하면 로그가 터미널에도 나온다
        if (args.Length == 2 && args[0] == "--make-ico") { TrayIcons.WriteIco(args[1], Core.TrayIconKind.Ready); return; }
        if (args.Length == 2 && args[0] == "--make-png")   // 개발용: 아이콘 크기별 미리보기 PNG
        {
            foreach (var kind in Enum.GetValues<Core.TrayIconKind>())
                foreach (var size in new[] { 16, 20, 24, 32 })
                {
                    using var b = TrayIcons.Render(kind, size);
                    b.Save(Path.Combine(args[1], $"{kind}-{size}.png"), System.Drawing.Imaging.ImageFormat.Png);
                }
            return;
        }
        if (args.Length == 2 && args[0] == "--guide-shot")   // 개발용: 안내 창을 이미지로 저장
        {
            ApplicationConfiguration.Initialize();
            var fake = new FakePhoneLink(_ => { });
            using var form = new GuideForm(fake);
            form.Show();
            foreach (var (name, problem, warn) in new[] {
                ("unauthorized", PhoneEdge.Contracts.PhoneLinkProblem.Unauthorized, PhoneEdge.Contracts.PhoneLinkWarnings.PointerAcceleration),
                ("debuggingoff", PhoneEdge.Contracts.PhoneLinkProblem.DebuggingOff, PhoneEdge.Contracts.PhoneLinkWarnings.None),
                ("ready", PhoneEdge.Contracts.PhoneLinkProblem.None, PhoneEdge.Contracts.PhoneLinkWarnings.None) })
            {
                fake.SetDiagnosis(problem, warn);
                form.Apply(fake.State, fake.Diagnosis);
                Application.DoEvents();
                using var bmp = new System.Drawing.Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                bmp.Save(Path.Combine(args[1], $"guide-{name}.png"));
            }
            return;
        }
        using var mutex =new Mutex(true, @"Local\PhoneEdge.SingleInstance", out bool first);
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
            MessageBox.Show(ex.Message, "PhoneEdge", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try { Application.Run(_app); }
        finally { _app.Shutdown(); }
    }
}
