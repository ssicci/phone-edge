using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using PhoneEdge.Contracts;
using PhoneEdge.Phone.Adb;
using PhoneEdge.Phone.Diagnostics;

namespace PhoneEdge.Phone;

/// <summary>
/// USB/ADB 전송 통로. scrcpy 서버를 영상·음성 없이 제어 소켓만으로 띄우고,
/// UHID 가상 마우스·키보드로 입력을 넣는다.
///
/// 스레드 구조:
/// - Send*/Enter/Leave: 명령을 무할당 구조체로 채널에 넣고 즉시 반환.
/// - 작업 스레드 1개: 채널을 비우며 커서 추적·소켓 쓰기 (PhoneInputProcessor).
/// - 감시 루프 1개: 폰 찾기 → 서버 실행 → 연결 → 끊김 감지 → 재시도.
/// </summary>
public sealed class AdbPhoneLink : IPhoneLink
{
    private readonly string _serverPath;
    private readonly AdbPhoneLinkOptions _options;
    private readonly AdbRunner _adb;
    private readonly Channel<Cmd> _queue = Channel.CreateUnbounded<Cmd>(new UnboundedChannelOptions { SingleReader = true });
    private readonly PhoneInputProcessor _processor;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _worker;
    private Task? _supervisor;
    private volatile Session? _session;
    private int _state = (int)PhoneLinkState.Disconnected;
    private readonly IUsbDeviceProbe _usb;
    private readonly object _diagLock = new();
    private PhoneLinkDiagnosis _diagnosis = PhoneLinkDiagnosis.Healthy;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private volatile string? _lastSerial;

    public AdbPhoneLink(string adbPath, string scrcpyServerPath, AdbPhoneLinkOptions? options = null, IUsbDeviceProbe? usbProbe = null)
    {
        _usb = usbProbe ?? new WindowsUsbDeviceProbe();
        if (!File.Exists(adbPath)) throw new FileNotFoundException("adb 를 찾을 수 없습니다", adbPath);
        if (!File.Exists(scrcpyServerPath)) throw new FileNotFoundException("scrcpy-server 를 찾을 수 없습니다", scrcpyServerPath);
        _serverPath = scrcpyServerPath;
        _options = options ?? new AdbPhoneLinkOptions();
        var log = _options.Log ?? (m => Debug.WriteLine("[PhoneEdge.Phone] " + m));
        _adb = new AdbRunner(adbPath, log);
        _processor = new PhoneInputProcessor(WriteToSession, RaiseExit, Thread.Sleep);
        _processor.Tracker.Gain = _options.CursorGain;
        _processor.Tracker.ExitThreshold = _options.ExitThreshold;
        _worker = Task.Factory.StartNew(WorkerLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    }

    public PhoneLinkState State => (PhoneLinkState)Volatile.Read(ref _state);

    public event Action<PhoneLinkState>? StateChanged;
    public event Action<double>? ExitRequested;

    /// <summary>현재 추정한 폰 화면 크기 (진단용).</summary>
    public (int Width, int Height) ScreenSize => (_processor.Tracker.Width, _processor.Tracker.Height);

    /// <summary>연결 때 측정한 커서 이득 (폰 px / 입력 카운트). 측정 실패면 null.</summary>
    public double? MeasuredGain { get; private set; }

    /// <summary>폰의 마우스 포인터 가속 설정. true 면 빠른 이동에서 추적이 어긋난다. 모르면 null.</summary>
    public bool? MouseAccelerationEnabled { get; private set; }

    // ---- 진단 ----

    public PhoneLinkDiagnosis Diagnosis { get { lock (_diagLock) return _diagnosis; } }

    /// <summary>Problem 이나 Warnings 가 바뀔 때만 발생 (같은 원인의 반복 판정은 이벤트를 내지 않는다).</summary>
    public event Action<PhoneLinkDiagnosis>? DiagnosisChanged;

    private void SetProblem(PhoneLinkProblem problem, string? detail)
        => UpdateDiagnosis(d => d with { Problem = problem, Detail = problem == PhoneLinkProblem.None ? null : detail });

    private void SetWarning(PhoneLinkWarnings flag, bool on)
        => UpdateDiagnosis(d => d with { Warnings = on ? d.Warnings | flag : d.Warnings & ~flag });

    private void UpdateDiagnosis(Func<PhoneLinkDiagnosis, PhoneLinkDiagnosis> change)
    {
        PhoneLinkDiagnosis next;
        lock (_diagLock)
        {
            next = change(_diagnosis);
            if (next.Problem == _diagnosis.Problem && next.Warnings == _diagnosis.Warnings) return;
            _diagnosis = next;
        }
        _adb.Log($"진단: {next.Problem} / {next.Warnings} {next.Detail}");
        try { DiagnosisChanged?.Invoke(next); } catch (Exception ex) { _adb.Log("DiagnosisChanged 구독자 오류: " + ex.Message); }
    }

    public async Task<bool> TryFixAsync(PhoneLinkFix fix)
    {
        switch (fix)
        {
            case PhoneLinkFix.Retry:
                bool ok = true;
                if (Diagnosis.Problem == PhoneLinkProblem.AdbFailed)
                {
                    _adb.Log("adb 서버 재시작");
                    try { await _adb.RunAsync(["kill-server"], TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false); }
                    catch (Exception ex) { _adb.Log("kill-server: " + ex.Message); }
                    try
                    {
                        var (code, outp) = await _adb.RunAsync(["start-server"], TimeSpan.FromSeconds(20), CancellationToken.None).ConfigureAwait(false);
                        ok = code == 0;
                        if (!ok) _adb.Log("start-server 실패: " + outp.Trim());
                    }
                    catch (Exception ex) { _adb.Log("start-server: " + ex.Message); ok = false; }
                }
                Wake();
                return ok;

            case PhoneLinkFix.DisablePointerAcceleration:
                string? serial = _session?.Serial ?? _lastSerial;
                if (serial == null) return false;
                try
                {
                    await _adb.RunAsync(["-s", serial, "shell", "settings put system " + AccelerationSettingKey + " 0"], TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false);
                    for (int i = 0; i < 10; i++) // 적용 확인 (다시 읽기)
                    {
                        var (_, line) = await _adb.RunAsync(["-s", serial, "shell", "dumpsys input | grep -m1 'Mouse pointer acceleration enabled'"], TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false);
                        if (AdbParsers.ParseMouseAccelerationEnabled(line) == false)
                        {
                            MouseAccelerationEnabled = false;
                            _queue.Writer.TryWrite(new Cmd(CmdKind.SetHomeSettle) { A = 0 });
                            SetWarning(PhoneLinkWarnings.PointerAcceleration, false);
                            return true;
                        }
                        await Task.Delay(200).ConfigureAwait(false);
                    }
                    _adb.Log("포인터 가속 끄기 후에도 켜져 있음");
                }
                catch (Exception ex) { _adb.Log("포인터 가속 끄기 실패: " + ex.Message); }
                return false;

            case PhoneLinkFix.DisableUsbDebugging:
                return await DisableUsbDebuggingAsync().ConfigureAwait(false);

            default:
                return false;
        }
    }

    /// <summary>Set after the app turned USB debugging off on purpose; the supervisor then does not report DebuggingOff.</summary>
    private volatile bool _debuggingIntentionallyOff;

    /// <summary>
    /// Turns USB debugging off with `settings put global adb_enabled 0` (called on app exit, so it must finish
    /// within about 3 s). adbd stops right after the setting changes, so the shell may end with an error or
    /// hang; success is decided by whether the command ran or the phone left adb afterwards.
    /// </summary>
    private async Task<bool> DisableUsbDebuggingAsync()
    {
        var s = _session;
        if (State != PhoneLinkState.Ready || s == null) return false;
        var sw = Stopwatch.StartNew();
        _debuggingIntentionallyOff = true; // before the command, so the supervisor never reports the drop
        bool ok;
        try
        {
            var (code, outp) = await _adb.RunAsync(["-s", s.Serial, "shell", "settings put global adb_enabled 0"],
                TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
            _adb.Log($"adb_enabled 0: exit {code} {outp.Trim()} ({sw.ElapsedMilliseconds} ms)");
            ok = code == 0 || await PhoneLeftAdbAsync(s.Serial).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _adb.Log($"adb_enabled 0: {ex.Message} ({sw.ElapsedMilliseconds} ms)");
            ok = await PhoneLeftAdbAsync(s.Serial).ConfigureAwait(false);
        }
        if (!ok) _debuggingIntentionallyOff = false;
        _adb.Log($"DisableUsbDebugging = {ok} ({sw.ElapsedMilliseconds} ms)");
        return ok;
    }

    /// <summary>True when adb no longer reports the phone as "device" (debugging went off).</summary>
    private async Task<bool> PhoneLeftAdbAsync(string serial)
    {
        try
        {
            var (_, state) = await _adb.RunAsync(["-s", serial, "get-state"], TimeSpan.FromMilliseconds(800), CancellationToken.None).ConfigureAwait(false);
            return state.Trim() != "device";
        }
        catch (TimeoutException) { return false; }
    }

    /// <summary>안드로이드 16+ 마우스 포인터 가속 설정 키 (0단계 실측: 0 이면 이득이 속도와 무관하게 고정).</summary>
    internal const string AccelerationSettingKey = "mouse_pointer_acceleration_enabled";

    private void Wake()
    {
        try { _wake.Release(); } catch (SemaphoreFullException) { }
    }

    /// <summary>폴링 대기. TryFixAsync(Retry) 로 바로 깨어난다.</summary>
    private Task SleepAsync(TimeSpan delay, CancellationToken ct) => _wake.WaitAsync(delay, ct);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_supervisor != null) return Task.CompletedTask;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        _supervisor = Task.Run(() => SupervisorLoop(cts.Token));
        return Task.CompletedTask;
    }

    // ---- 입력 (훅 스레드, 즉시 반환) ----

    public void Enter(double yRatio)
    {
        if (State == PhoneLinkState.Ready) _queue.Writer.TryWrite(new Cmd(CmdKind.Enter) { Y = yRatio });
    }

    public void Leave()
    {
        if (State == PhoneLinkState.Ready) _queue.Writer.TryWrite(new Cmd(CmdKind.Leave));
    }

    public void SendMouse(int dx, int dy, PhoneMouseButtons buttons, int wheel)
    {
        if (State == PhoneLinkState.Ready)
            _queue.Writer.TryWrite(new Cmd(CmdKind.Mouse) { A = dx, B = dy, C = (int)buttons, D = wheel });
    }

    public void SendKeyboard(byte modifiers, ReadOnlySpan<byte> keys)
    {
        if (State != PhoneLinkState.Ready) return;
        ulong packed = 0;
        int n = Math.Min(keys.Length, 6);
        for (int i = 0; i < n; i++) packed |= (ulong)keys[i] << (8 * i);
        _queue.Writer.TryWrite(new Cmd(CmdKind.Keyboard) { C = modifiers, Keys = packed });
    }

    // ---- 작업 스레드 ----

    private enum CmdKind : byte { Enter, Leave, Mouse, Keyboard, Resize, CreateDevices, Reset, CalibrateHome, CalibrateStep, SetGain, SetHomeSettle }

    private readonly record struct Cmd(CmdKind Kind)
    {
        public int A { get; init; }
        public int B { get; init; }
        public int C { get; init; }
        public int D { get; init; }
        public double Y { get; init; }
        public ulong Keys { get; init; }
    }

    private async Task WorkerLoop()
    {
        var reader = _queue.Reader;
        var keys = new byte[6];
        try
        {
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (reader.TryRead(out var c))
                {
                    try
                    {
                        switch (c.Kind)
                        {
                            case CmdKind.Enter: _processor.Enter(c.Y); break;
                            case CmdKind.Leave: _processor.Leave(); break;
                            case CmdKind.Mouse: _processor.Mouse(c.A, c.B, (PhoneMouseButtons)c.C, c.D); break;
                            case CmdKind.Keyboard:
                                for (int i = 0; i < 6; i++) keys[i] = (byte)(c.Keys >> (8 * i));
                                _processor.Keyboard((byte)c.C, keys);
                                break;
                            case CmdKind.Resize: _processor.Resize(c.A, c.B); break;
                            case CmdKind.CreateDevices: _processor.CreateDevices(); break;
                            case CmdKind.Reset: _processor.Reset(); break;
                            case CmdKind.CalibrateHome: _processor.CalibrationHome(); break;
                            case CmdKind.CalibrateStep: _processor.CalibrationStep(c.A); break;
                            case CmdKind.SetGain: _processor.Tracker.Gain = c.Y; break;
                            case CmdKind.SetHomeSettle: _processor.HomeSettleMs = c.A; break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _adb.Log("입력 처리 오류: " + ex.Message);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is ChannelClosedException or OperationCanceledException) { }
    }

    private void WriteToSession(byte[] data)
    {
        var s = _session;
        if (s?.Stream == null) return;
        try
        {
            s.Stream.Write(data);
        }
        catch (Exception ex)
        {
            _adb.Log("제어 소켓 쓰기 실패: " + ex.Message);
            s.Lost.TrySetResult();
        }
    }

    private void RaiseExit(double yRatio)
    {
        var handler = ExitRequested;
        if (handler != null) ThreadPool.QueueUserWorkItem(_ => handler(yRatio));
    }

    private void SetState(PhoneLinkState s)
    {
        if (Interlocked.Exchange(ref _state, (int)s) == (int)s) return;
        _adb.Log("상태: " + s);
        try { StateChanged?.Invoke(s); } catch (Exception ex) { _adb.Log("StateChanged 구독자 오류: " + ex.Message); }
    }

    // ---- 감시 루프 ----

    private async Task SupervisorLoop(CancellationToken ct)
    {
        int adbFailures = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var verdict = await FindDeviceAsync(ct).ConfigureAwait(false);
                // adb 서버가 재시작되는 순간(kill-server 직후 등)에는 한 번쯤 통신 실패가 난다 (실측: connection reset).
                // 연속 실패일 때만 AdbFailed 로 알리고, 첫 실패는 짧게 쉬고 다시 본다.
                adbFailures = verdict.Problem == PhoneLinkProblem.AdbFailed ? adbFailures + 1 : 0;
                if (adbFailures == 1)
                {
                    _adb.Log("adb 통신 실패, 곧 다시 확인: " + verdict.Detail);
                    await SleepAsync(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
                    continue;
                }
                bool wasOff = _debuggingIntentionallyOff;
                (verdict, bool stillOff) = LinkDiagnostics.ApplyIntentionalDebuggingOff(verdict, wasOff);
                if (wasOff && !stillOff) { _debuggingIntentionallyOff = false; _adb.Log("USB debugging is visible again"); }
                if (verdict.Problem != PhoneLinkProblem.None || verdict.Serial == null)
                {
                    if (verdict.Problem != PhoneLinkProblem.None || stillOff) SetProblem(verdict.Problem, verdict.Detail);
                    await SleepAsync(_options.PollInterval, ct).ConfigureAwait(false);
                    continue;
                }

                _lastSerial = verdict.Serial;
                SetState(PhoneLinkState.Connecting);
                Session session;
                try
                {
                    session = await ConnectAsync(verdict.Serial, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    _adb.Log("연결 실패: " + ex.Message);
                    SetProblem(ex is LinkFailureException lf ? lf.Problem : PhoneLinkProblem.ServerFailed, ex.Message);
                    SetState(PhoneLinkState.Disconnected);
                    await SleepAsync(_options.PollInterval, ct).ConfigureAwait(false);
                    continue;
                }

                SetProblem(PhoneLinkProblem.None, null);
                SetState(PhoneLinkState.Ready);
                try
                {
                    await session.Lost.Task.WaitAsync(ct).ConfigureAwait(false);
                }
                finally
                {
                    _queue.Writer.TryWrite(new Cmd(CmdKind.Reset));
                    _session = null;
                    SetState(PhoneLinkState.Disconnected);
                    await session.DisposeAsync(_adb).ConfigureAwait(false);
                }
                await SleepAsync(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _adb.Log("감시 루프 오류: " + ex.Message);
                try { await SleepAsync(_options.PollInterval, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            }
        }
        SetState(PhoneLinkState.Disconnected);
    }

    /// <summary>`adb devices -l` 과 (adb 에 아무것도 없을 때만) Windows USB 장치 목록으로 폰 찾기 단계를 판정한다.</summary>
    private async Task<DeviceVerdict> FindDeviceAsync(CancellationToken ct)
    {
        AdbDevicesResult result;
        try
        {
            var (code, output) = await _adb.RunAsync(["devices", "-l"], TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
            result = new AdbDevicesResult(code, output, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            result = new AdbDevicesResult(null, "", ex.GetType().Name + ": " + ex.Message);
        }
        bool needUsb = result.Error == null && result.ExitCode == 0 && AdbParsers.ParseDevices(result.Output).Count == 0;
        return LinkDiagnostics.ClassifyDevices(result, needUsb ? _usb.GetPresentUsbDeviceIds() : []);
    }

    private async Task<Session> ConnectAsync(string serial, CancellationToken ct)
    {
        var s = new Session(serial);
        try
        {
            var (code, outp) = await _adb.RunAsync(["-s", serial, "push", _serverPath, _options.DeviceServerPath], TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            if (code != 0) throw new LinkFailureException(PhoneLinkProblem.ServerFailed, "서버 push 실패: " + outp.Trim());

            int scid = Random.Shared.Next(1, int.MaxValue);
            string socket = "scrcpy_" + scid.ToString("x8", CultureInfo.InvariantCulture);
            s.Port = FreeTcpPort();
            (code, outp) = await _adb.RunAsync(["-s", serial, "forward", "tcp:" + s.Port, "localabstract:" + socket], TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
            if (code != 0) throw new LinkFailureException(PhoneLinkProblem.ServerFailed, "adb forward 실패: " + outp.Trim());

            s.Server = _adb.StartLong(
            [
                "-s", serial, "shell",
                "CLASSPATH=" + _options.DeviceServerPath, "app_process", "/", "com.genymobile.scrcpy.Server", _options.ServerVersion,
                "scid=" + scid.ToString("x8", CultureInfo.InvariantCulture),
                "log_level=info", "video=false", "audio=false", "control=true", "tunnel_forward=true",
                "send_device_meta=false", "send_dummy_byte=true", "clipboard_autosync=false", "cleanup=true", "power_on=false",
            ], line => _adb.Log("[server] " + line));
            s.Server.Exited += (_, _) => s.Lost.TrySetResult();

            await ConnectSocketAsync(s, ct).ConfigureAwait(false);
            _session = s;
            _ = Task.Run(() => ReadDeviceMessages(s));
            _queue.Writer.TryWrite(new Cmd(CmdKind.CreateDevices));
            await Task.Delay(_options.DeviceSettleDelay, ct).ConfigureAwait(false);

            // 마우스 커서가 있는 화면 크기 + 가속 설정 (Enter 보다 먼저 처리되도록 Ready 전에 큐에 넣는다)
            var (_, dump) = await _adb.RunAsync(["-s", serial, "shell", "dumpsys input"], TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
            if (!HasInputDevices(dump))
            {
                await Task.Delay(1000, ct).ConfigureAwait(false); // 등록이 늦을 수 있어 한 번 더
                (_, dump) = await _adb.RunAsync(["-s", serial, "shell", "dumpsys input"], TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
                if (!HasInputDevices(dump))
                    throw new LinkFailureException(PhoneLinkProblem.InputDeviceFailed,
                        "가상 입력 장치가 폰 입력 목록에 없음" + (s.Lost.Task.IsCompleted ? " (서버 종료됨)" : ""));
            }
            if (AdbParsers.TryParseMouseViewport(dump, out int displayId, out int w, out int h))
            {
                s.LastSize = (w, h);
                _queue.Writer.TryWrite(new Cmd(CmdKind.Resize) { A = w, B = h });
                _adb.Log($"마우스 화면: display {displayId} {w}x{h}");
            }
            else _adb.Log("마우스 화면 크기를 읽지 못함, 기본값 사용");

            MouseAccelerationEnabled = AdbParsers.ParseMouseAccelerationEnabled(dump);
            SetWarning(PhoneLinkWarnings.PointerAcceleration, MouseAccelerationEnabled == true);
            _queue.Writer.TryWrite(new Cmd(CmdKind.SetHomeSettle) { A = MouseAccelerationEnabled == false ? 0 : (int)_options.HomeSettleDelay.TotalMilliseconds });
            if (MouseAccelerationEnabled == true)
                _adb.Log("경고: 폰의 마우스 포인터 가속이 켜져 있음. 빠르게 움직이면 커서 추적이 어긋난다 (폰 설정에서 끄기 권장)");

            if (_options.AutoCalibrate) await CalibrateAsync(s, w, ct).ConfigureAwait(false);

            // 접기/펼치기/회전 감시: 1초마다 마우스 화면 viewport 출력
            s.Watcher = _adb.StartLong(
                ["-s", serial, "shell", "while true; do dumpsys input | grep -m1 'viewport: Viewport'; sleep 1; done"],
                line =>
                {
                    if (AdbParsers.TryParseMouseViewport(line, out _, out int ww, out int hh) && (ww, hh) != s.LastSize)
                    {
                        _adb.Log($"화면 크기 변경: {s.LastSize} → ({ww}, {hh})");
                        s.LastSize = (ww, hh);
                        _queue.Writer.TryWrite(new Cmd(CmdKind.Resize) { A = ww, B = hh });
                    }
                });

            if (s.Lost.Task.IsCompleted) throw new LinkFailureException(PhoneLinkProblem.ServerFailed, "서버가 바로 종료됨");
            return s;
        }
        catch
        {
            if (_session == s) _session = null;
            await s.DisposeAsync(_adb).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 이득 자동 측정: 좌상단에 붙인 뒤 CalibrationCounts 를 한 리포트로 보내고 실제 포인터 x 를 읽는다.
    /// 단일 리포트는 가속이 붙지 않으므로 가속 설정과 무관하게 기본 이득이 나온다.
    /// </summary>
    private async Task CalibrateAsync(Session s, int width, CancellationToken ct)
    {
        int counts = _options.CalibrationCounts;
        _queue.Writer.TryWrite(new Cmd(CmdKind.CalibrateHome));
        await Task.Delay(600, ct).ConfigureAwait(false); // 가속 속도 추정이 식을 때까지
        _queue.Writer.TryWrite(new Cmd(CmdKind.CalibrateStep) { A = counts });
        await Task.Delay(400, ct).ConfigureAwait(false);
        var (_, line) = await _adb.RunAsync(["-s", s.Serial, "shell", "dumpsys input | grep -m1 'MOUSE) at'"], TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
        if (AdbParsers.TryParseMousePosition(line, out double x, out _) && x > 0 && (width == 0 || x < width - 1))
        {
            MeasuredGain = x / counts;
            _queue.Writer.TryWrite(new Cmd(CmdKind.SetGain) { Y = MeasuredGain.Value });
            _adb.Log($"커서 이득 측정: {counts} 카운트 → {x:F1} px, 이득 {MeasuredGain:F3}");
        }
        else _adb.Log($"커서 이득 측정 실패, 설정값 {_options.CursorGain} 사용 ({line.Trim()})");
    }

    private static bool HasInputDevices(string dump)
        => LinkDiagnostics.HasVirtualInputDevices(dump, PhoneInputProcessor.MouseName, PhoneInputProcessor.KeyboardName);

    private static async Task ConnectSocketAsync(Session s, CancellationToken ct)
    {
        // adb forward 는 서버가 아직 listen 전이어도 TCP 연결을 받아 준 뒤 바로 닫는다.
        // 그래서 서버가 보내는 dummy byte 1개를 받아야 진짜 연결로 본다.
        var buf = new byte[1];
        for (int attempt = 0; attempt < 100; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            if (s.Lost.Task.IsCompleted) throw new LinkFailureException(PhoneLinkProblem.ServerFailed, "서버 프로세스가 종료됨 (로그 확인)");
            var tcp = new TcpClient { NoDelay = true };
            try
            {
                await tcp.ConnectAsync(IPAddress.Loopback, s.Port, ct).ConfigureAwait(false);
                var stream = tcp.GetStream();
                using var t = CancellationTokenSource.CreateLinkedTokenSource(ct);
                t.CancelAfter(TimeSpan.FromSeconds(2));
                int n = await stream.ReadAsync(buf, t.Token).ConfigureAwait(false);
                if (n == 1)
                {
                    s.Tcp = tcp;
                    s.Stream = stream;
                    return;
                }
            }
            catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException && !ct.IsCancellationRequested) { }
            tcp.Dispose();
            await Task.Delay(100, ct).ConfigureAwait(false);
        }
        throw new LinkFailureException(PhoneLinkProblem.ServerFailed, "scrcpy 서버 소켓 연결 시간 초과");
    }

    /// <summary>폰 → PC 메시지 (키보드 LED 등). 내용은 쓰지 않고, 끊김(EOF) 감지에만 쓴다.</summary>
    private async Task ReadDeviceMessages(Session s)
    {
        var buf = new byte[4096];
        try
        {
            while (s.Stream != null && await s.Stream.ReadAsync(buf).ConfigureAwait(false) > 0) { }
            _adb.Log("제어 소켓 닫힘");
        }
        catch (Exception ex)
        {
            _adb.Log("제어 소켓 읽기 종료: " + ex.Message);
        }
        s.Lost.TrySetResult();
    }

    private static int FreeTcpPort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private sealed class Session(string serial)
    {
        public string Serial { get; } = serial;
        public int Port { get; set; }
        public Process? Server { get; set; }
        public Process? Watcher { get; set; }
        public TcpClient? Tcp { get; set; }
        public NetworkStream? Stream { get; set; }
        public (int, int) LastSize { get; set; }
        public TaskCompletionSource Lost { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task DisposeAsync(AdbRunner adb)
        {
            Lost.TrySetResult();
            try { Tcp?.Dispose(); } catch { }
            AdbRunner.TryKill(Watcher);
            AdbRunner.TryKill(Server);
            if (Port != 0)
            {
                // 재연결을 막지 않도록 기다리지 않는다 (adb 서버가 죽었거나 기기가 빠졌으면 수 초 걸린다)
                string serial = Serial, port = "tcp:" + Port;
                _ = Task.Run(async () =>
                {
                    try { await adb.RunAsync(["-s", serial, "forward", "--remove", port], TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false); }
                    catch { /* 기기가 이미 빠짐 */ }
                });
            }
            Watcher?.Dispose();
            Server?.Dispose();
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_lifetime.IsCancellationRequested) return;
        _lifetime.Cancel();
        if (_supervisor != null)
        {
            try { await _supervisor.ConfigureAwait(false); } catch { }
        }
        _queue.Writer.TryComplete();
        try { await _worker.ConfigureAwait(false); } catch { }
        _lifetime.Dispose();
    }
}
