// 0단계 스파이크: 폰을 USB 로 연결하고 실행한다.
//   dotnet run --project spike/PhoneEdge.Spike -- [demo|gain|exit]
// demo : 커서 진입 → 사각형 이동 → 클릭 → "hello" 입력 (항목 b)
// gain : 좌측 끝에서 +N 카운트를 천천히/빠르게 보낸 뒤 dumpsys input 의 실제 포인터 위치를 읽어 이득·가속을 잰다 (항목 c)
// exit : 오른쪽으로 400 이동 후 왼쪽으로 조금씩 밀어 ExitRequested 가 몇 카운트에서 뜨는지 잰다 (항목 c)
using System.Diagnostics;
using System.Text.RegularExpressions;
using PhoneEdge.Contracts;
using PhoneEdge.Phone;

string root = FindRoot();
string adb = Path.Combine(root, "tools", "platform-tools", "adb.exe");
string server = Path.Combine(root, "tools", "scrcpy-server");
string mode = args.Length > 0 ? args[0] : "demo";
double gain = args.Length > 1 ? double.Parse(args[1]) : 2.68;

await using var link = new AdbPhoneLink(adb, server, new AdbPhoneLinkOptions { Log = m => Console.WriteLine("  · " + m), CursorGain = gain, CompanionApkPath = Environment.GetEnvironmentVariable("PHONEEDGE_COMPANION_APK") });
var ready = new TaskCompletionSource();
link.StateChanged += s => { if (s == PhoneLinkState.Ready) ready.TrySetResult(); };
double? exitY = null;
link.ExitRequested += y => { exitY = y; Console.WriteLine($"ExitRequested y={y:F3}"); };
await link.StartAsync(CancellationToken.None);

if (mode == "diag") // 진단 관찰: diag <초> [retry|fixaccel]  — retry 는 절반 시점, fixaccel 은 Ready 직후
{
    var clock = Stopwatch.StartNew();
    void Print(string what) => Console.WriteLine($"[{clock.Elapsed.TotalSeconds,5:F1}s] {what}");
    Print($"진단 {link.Diagnosis}");
    link.DiagnosisChanged += d => Print($"DiagnosisChanged {d.Problem} / {d.Warnings} / {d.Detail}");
    link.StateChanged += s => Print($"StateChanged {s}");
    double seconds = args.Length > 1 ? double.Parse(args[1]) : 20;
    string action = args.Length > 2 ? args[2] : "";
    if (action == "fixaccel")
    {
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(seconds));
        Print($"TryFixAsync(DisablePointerAcceleration) = {await link.TryFixAsync(PhoneLinkFix.DisablePointerAcceleration)}");
    }
    else if (action is "companion" or "installcompanion") // PHONEEDGE_COMPANION_APK=<apk>
    {
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(seconds));
        for (int i = 0; i < 50 && link.Companion == CompanionState.Unknown; i++) await Task.Delay(100);
        Print($"Companion = {link.Companion}");
        if (action == "installcompanion")
        {
            var t = Stopwatch.StartNew();
            bool r = await link.TryFixAsync(PhoneLinkFix.InstallCompanion);
            Print($"TryFixAsync(InstallCompanion) = {r} ({t.ElapsedMilliseconds} ms), Companion = {link.Companion}");
        }
    }
    else if (action == "offusb")
    {
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(seconds));
        var t = Stopwatch.StartNew();
        bool r = await link.TryFixAsync(PhoneLinkFix.DisableUsbDebugging);
        Print($"TryFixAsync(DisableUsbDebugging) = {r} ({t.ElapsedMilliseconds} ms)");
    }
    else if (action == "retry")
    {
        await Task.Delay(TimeSpan.FromSeconds(seconds / 2));
        Print($"TryFixAsync(Retry) = {await link.TryFixAsync(PhoneLinkFix.Retry)}");
    }
    await Task.Delay(TimeSpan.FromSeconds(seconds) - clock.Elapsed is var rest && rest > TimeSpan.Zero ? rest : TimeSpan.Zero);
    Print($"끝: {link.State}, {link.Diagnosis}");
    return;
}

Console.WriteLine("폰 연결 대기 (USB 디버깅 허용 필요)...");
await ready.Task.WaitAsync(TimeSpan.FromMinutes(2));
Console.WriteLine($"Ready, 화면 {link.ScreenSize}, 이득 {link.MeasuredGain}, 가속 {link.MouseAccelerationEnabled}");
gain = link.MeasuredGain ?? gain;
var edge = args.Contains("right") ? PhoneEntryEdge.Right : args.Contains("top") ? PhoneEntryEdge.Top
         : args.Contains("bottom") ? PhoneEntryEdge.Bottom : PhoneEntryEdge.Left;
bool vertical = edge is PhoneEntryEdge.Top or PhoneEntryEdge.Bottom;

switch (mode)
{
    case "demo":
        link.Enter(0.5);
        await Task.Delay(300);
        foreach (var (dx, dy) in new[] { (10, 0), (0, 10), (-10, 0), (0, -10) })
            for (int i = 0; i < 30; i++) { link.SendMouse(dx, dy, 0, 0); await Task.Delay(8); }
        link.SendMouse(200, 0, 0, 0);
        await Task.Delay(300);
        link.SendMouse(0, 0, PhoneMouseButtons.Left, 0); await Task.Delay(50);
        link.SendMouse(0, 0, PhoneMouseButtons.None, 0); await Task.Delay(500);
        foreach (byte k in new byte[] { 0x0B, 0x08, 0x0F, 0x0F, 0x12 }) // h e l l o
        {
            link.SendKeyboard(0, [k]); await Task.Delay(30);
            link.SendKeyboard(0, []); await Task.Delay(30);
        }
        Console.WriteLine("커서가 보이고 움직였는지, 클릭과 'hello' 입력이 됐는지 폰에서 확인하세요.");
        break;

    case "gain":
        foreach (int stepDelay in new[] { 16, 2 })
        {
            link.Enter(0.5);
            await Task.Delay(500);
            var before = await ReadPointer();
            for (int i = 0; i < 50; i++) { link.SendMouse(4, 0, 0, 0); await Task.Delay(stepDelay); }
            await Task.Delay(500);
            var after = await ReadPointer();
            Console.WriteLine($"간격 {stepDelay}ms, 입력 200 카운트: 전 {before} 후 {after}");
            link.Leave();
        }
        Console.WriteLine("(포인터 위치를 못 읽으면 dumpsys input 원문에서 포인터 좌표 줄을 확인)");
        break;

    case "dump": // 진입 후 +100 카운트 이동한 상태의 dumpsys input 원문 저장: dump <gain> <파일>
        link.Enter(0.5);
        await Task.Delay(500);
        for (int i = 0; i < 25; i++) { link.SendMouse(4, 0, 0, 0); await Task.Delay(16); }
        await Task.Delay(500);
        File.WriteAllText(args[2], await RunAdb("shell dumpsys input"));
        Console.WriteLine("저장: " + args[2]);
        break;

    case "curve": // 좌상단 고정 후 (step, 간격, 횟수) 조합별 실제 x 이동 → 이득 표
        foreach (var (step, ms, n) in new[] { (1, 16, 100), (2, 16, 100), (4, 16, 50), (8, 16, 25), (4, 4, 50), (10, 8, 20), (20, 8, 10), (200, 0, 1), (1, 2, 200) })
        {
            link.Enter(0);
            await Task.Delay(400);
            for (int i = 0; i < n; i++) { link.SendMouse(step, 0, 0, 0); if (ms > 0) await Task.Delay(ms); }
            await Task.Delay(400);
            string line = await RunAdb("shell \"dumpsys input | grep -m1 'MOUSE) at'\"");
            if (PhoneEdge.Phone.Adb.AdbParsers.TryParseMousePosition(line, out double px, out double py))
                Console.WriteLine($"step {step,3} @{ms,2}ms x{n,3} = {step * n,4} 카운트 → x={px,7:F1} y={py,6:F1}  이득 {px / (step * n):F3}  속도 {(ms > 0 ? step * 1000.0 / ms : double.PositiveInfinity):F0} cnt/s");
            else Console.WriteLine("위치 읽기 실패: " + line.Trim());
            link.Leave();
        }
        break;

    case "accel": // 정확한 주기(바쁜 대기)로 속도별 이득 측정: accel <gain> <주기ms>
    {
        double period = args.Length > 2 ? double.Parse(args[2]) : 8;
        foreach (int step in new[] { 2, 5, 10, 15, 20, 25, 30, 40, 50, 60, 75 })
        {
            int n = Math.Max(4, 300 / step);
            link.Enter(0);
            await Task.Delay(400);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < n; i++)
            {
                while (sw.Elapsed.TotalMilliseconds < i * period) Thread.SpinWait(50);
                link.SendMouse(step, 0, 0, 0);
            }
            await Task.Delay(400);
            string line = await RunAdb("shell \"dumpsys input | grep -m1 'MOUSE) at'\"");
            if (PhoneEdge.Phone.Adb.AdbParsers.TryParseMousePosition(line, out double px, out _))
                Console.WriteLine($"속도 {step * 1000 / period,6:F0} cnt/s (step {step,2} x{n,3}) → 이득 {px / (step * n):F3}{(px >= 1960 ? "  (벽 닿음)" : "")}");
            link.Leave();
        }
        break;
    }

    case "enter": // Enter(r, edge) 후 실제 포인터 위치(화면 좌표로 변환) 확인: enter <gain> [right|top|bottom]
        foreach (double r in new[] { 0.2, 0.5, 0.8 })
        {
            link.Enter(r, edge);
            await Task.Delay(500);
            var (px, py) = await ScreenPointer();
            int w = link.ScreenSize.Width, h = link.ScreenSize.Height;
            double ex = edge switch { PhoneEntryEdge.Right => w - 1, PhoneEntryEdge.Left => 0, _ => r * (w - 1) };
            double ey = edge switch { PhoneEntryEdge.Bottom => h - 1, PhoneEntryEdge.Top => 0, _ => r * (h - 1) };
            Console.WriteLine($"Enter({r}, {edge}) → 실제 ({px:F0}, {py:F0}), 기대 ({ex:F0}, {ey:F0})");
            link.Leave();
        }
        break;

    case "exit": // exit <gain> [right|top|bottom]: 안쪽으로 이동 후 진입 가장자리 쪽으로 2 카운트씩 밀어 이탈 카운트를 잰다
    {
        int outward = edge is PhoneEntryEdge.Right or PhoneEntryEdge.Bottom ? 1 : -1;
        int axisLen = vertical ? link.ScreenSize.Height : link.ScreenSize.Width;
        int inward = Math.Min(400, (int)(axisLen * 0.6 / gain) / 10 * 10); // 반대쪽 벽에 닿지 않게
        link.Enter(0.5, edge);
        await Task.Delay(300);
        void Push(int c) { if (vertical) link.SendMouse(0, c, 0, 0); else link.SendMouse(c, 0, 0, 0); }
        for (int i = 0; i < inward / 10; i++) { Push(-10 * outward); await Task.Delay(8); }
        await Task.Delay(300);
        var (mx, my) = await ScreenPointer();
        int pushed = 0;
        while (exitY == null && pushed < 2000) { Push(2 * outward); pushed += 2; await Task.Delay(8); }
        Console.WriteLine($"{edge}: 안쪽 {inward} (실제 ({mx:F0}, {my:F0})) → 바깥쪽 {pushed} 카운트에서 이탈 (이론값 {inward + 24 / gain:F0}), 비율 {exitY:F3}");
        break;
    }
}
await Task.Delay(500);

async Task<string> RunAdb(string arguments)
{
    var psi = new ProcessStartInfo(adb, arguments) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
    using var p = Process.Start(psi)!;
    return await p.StandardOutput.ReadToEndAsync();
}

async Task<(double X, double Y)> ScreenPointer() // 호버 위치를 viewport 회전에 맞춰 화면 좌표로
{
    string text = await RunAdb("shell \"dumpsys input | grep -E 'viewport: Viewport|MOUSE\\) at'\"");
    PhoneEdge.Phone.Adb.AdbParsers.TryParseMousePosition(text, out double hx, out double hy);
    int o = PhoneEdge.Phone.Adb.AdbParsers.ParseMouseOrientation(text) ?? 0;
    return PhoneEdge.Phone.Adb.AdbParsers.HoverToScreen(hx, hy, o, link.ScreenSize.Width, link.ScreenSize.Height);
}

async Task<string> ReadPointer()
{
    string text = await RunAdb("shell dumpsys input");
    var lines = text.Split('\n').Where(l => Regex.IsMatch(l, @"(?i)(position|pointer.*[xy]\s*=|mLocked|CursorPosition)")).Take(6);
    return "[" + string.Join(" | ", lines.Select(l => l.Trim())) + "]";
}

static string FindRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d != null && !File.Exists(Path.Combine(d.FullName, "CLAUDE.md"))) d = d.Parent;
    return d?.FullName ?? throw new InvalidOperationException("phone-edge 루트를 찾지 못함");
}
