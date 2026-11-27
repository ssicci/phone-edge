// Spike: the laptop's built-in Bluetooth as a BLE HID (HID over GATT) mouse + keyboard for the phone.
// Run the published exe from Explorer. Every step goes to the console and to spike/bt-hid-result.txt.
// Enter quits.
using System.Diagnostics;
using System.Text;
using PhoneEdge.Contracts;
using PhoneEdge.Phone.Protocol;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

Console.OutputEncoding = Encoding.UTF8;
var log = new Log(ResultPath());
log.Write($"PhoneEdge BLE HID spike start. Windows {Environment.OSVersion.Version}, result file: {log.Path}");

try
{
    await new HidPeripheral(log).RunAsync();
}
catch (Exception e)
{
    log.Write("FATAL " + e);
}
log.Write("end");
log.Dispose();
Console.WriteLine("Press Enter to close this window.");
Console.ReadLine();

static string ResultPath()
{
    // spike/out/PhoneEdge.BtHidSpike.exe → spike/bt-hid-result.txt (falls back next to the exe)
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    for (var p = d; p != null; p = p.Parent)
        if (p.Name.Equals("spike", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(p.Parent?.FullName ?? "", "CLAUDE.md")))
            return Path.Combine(p.FullName, "bt-hid-result-3.txt");
    return Path.Combine(d.FullName, "bt-hid-result-3.txt");
}

sealed class Log : IDisposable
{
    private readonly StreamWriter _file;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly object _gate = new();
    public string Path { get; }

    public Log(string path)
    {
        Path = path;
        _file = new StreamWriter(path, append: false, new UTF8Encoding(false)) { AutoFlush = true };
    }

    public void Write(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff} +{_clock.Elapsed.TotalSeconds,7:F1}s] {message}";
        lock (_gate)
        {
            Console.WriteLine(line);
            _file.WriteLine(line);
        }
    }

    public void Dispose() => _file.Dispose();
}

sealed class HidPeripheral(Log log)
{
    // HID report IDs inside the combined Report Map (the UHID devices use none because they are separate devices).
    private const byte KeyboardReportId = 1;
    private const byte MouseReportId = 2;

    private readonly List<GattServiceProvider> _providers = [];
    private readonly Dictionary<string, SessionInfo> _sessions = [];
    private GattLocalCharacteristic? _mouseInput, _keyboardInput;
    private volatile bool _quit;
    private string _addressTail = "??:??";

    // Run 2 review: only the two input-report characteristics were kept in fields; Report Map, HID Information and the
    // others lived only in locals, so their managed wrappers (and the event handlers on them) could be collected.
    // Run 3 keeps every provider, characteristic, descriptor and watcher reachable for the whole run.
    private readonly List<object> _keepAlive = [];

    // Control service (run 3): random 128-bit UUIDs, one plain and one encrypted read+notify characteristic.
    private static readonly Guid ControlService = Guid.Parse("6c0e2a40-5b1d-4f0a-9d5e-3f6a1b2c7d01");
    private static readonly Guid ControlPlain = Guid.Parse("6c0e2a41-5b1d-4f0a-9d5e-3f6a1b2c7d01");
    private static readonly Guid ControlEncrypted = Guid.Parse("6c0e2a42-5b1d-4f0a-9d5e-3f6a1b2c7d01");
    private GattLocalCharacteristic? _ctlPlain, _ctlEncrypted;

    // Marker in the HID advertisement (Service Data for 0x1812) so a scanner app can tell this PC apart: ASCII "PhoneEdge".
    private static readonly byte[] Marker = Encoding.ASCII.GetBytes("PhoneEdge");

    private sealed class SessionInfo
    {
        public GattSession? Session;
        public BluetoothLEDevice? Device;
        public bool PairAttempted;
    }

    private static Guid Uuid(ushort shortId) => BluetoothUuidHelper.FromShortId(shortId);
    private static IBuffer Buf(params byte[] bytes) => CryptographicBuffer.CreateFromByteArray(bytes);
    private static string Hex(IBuffer b) { CryptographicBuffer.CopyToByteArray(b, out byte[] a); return Convert.ToHexString(a ?? []); }

    /// <summary>Keyboard and mouse descriptors from the UHID engine, merged into one map with a Report ID each.</summary>
    public static byte[] ReportMap()
    {
        static IEnumerable<byte> WithId(ReadOnlySpan<byte> d, byte id)
        {
            // Both descriptors start: Usage Page, Usage, Collection(Application) = 6 bytes. Report ID goes right after.
            var a = d.ToArray();
            return a.Take(6).Concat(new byte[] { 0x85, id }).Concat(a.Skip(6));
        }
        return [.. WithId(HidReports.KeyboardDescriptor, KeyboardReportId), .. WithId(HidReports.MouseDescriptor, MouseReportId)];
    }

    public async Task RunAsync()
    {
        var adapter = await BluetoothAdapter.GetDefaultAsync();
        if (adapter == null) { log.Write("STEP adapter: no Bluetooth adapter (or Bluetooth is off)"); return; }
        _addressTail = $"{(adapter.BluetoothAddress >> 8) & 0xFF:X2}:{adapter.BluetoothAddress & 0xFF:X2}";
        log.Write($"STEP adapter: public address ends with {_addressTail}");
        log.Write($"STEP adapter: LE={adapter.IsLowEnergySupported} peripheralRole={adapter.IsPeripheralRoleSupported} " +
                  $"central={adapter.IsCentralRoleSupported} LESecureConnections={adapter.AreLowEnergySecureConnectionsSupported} " +
                  $"extendedAdv={adapter.IsExtendedAdvertisingSupported}");
        if (!adapter.IsPeripheralRoleSupported) log.Write("WARN adapter says peripheral role is not supported; advertising will likely fail");

        StartLinkWatcher();
        await CreateControlServiceAsync();
        await CreateHidServiceAsync();
        await Task.Delay(1500);
        log.Write("STEP advertising after 1.5 s: " + string.Join(", ", _providers.Select(p => $"{p.Service.Uuid}={p.AdvertisementStatus}")));

        Guide();
        log.Write("Waiting for the phone to connect over LE. Press Enter here to quit.");
        var demo = Task.Run(DemoLoopAsync);
        var control = Task.Run(ControlNotifyLoopAsync);
        var status = Task.Run(StatusLoopAsync);
        await Task.Run(Console.ReadLine);
        _quit = true;
        await Task.WhenAll(demo, status, control);
        foreach (var p in _providers)
        {
            try { p.StopAdvertising(); } catch (Exception e) { log.Write("stop advertising: " + e.Message); }
        }
        foreach (var s in _sessions.Values) { s.Device?.Dispose(); s.Session?.Dispose(); }
    }

    // ---------------- services ----------------

    private Task<GattServiceProvider?> CreateProviderAsync(string name, ushort uuid) => CreateProviderAsync(name, Uuid(uuid));

    private async Task<GattServiceProvider?> CreateProviderAsync(string name, Guid uuid)
    {
        var r = await GattServiceProvider.CreateAsync(uuid);
        log.Write($"STEP service {name} ({uuid}) create: {r.Error}");
        if (r.Error != BluetoothError.Success) return null;
        var p = r.ServiceProvider;
        p.AdvertisementStatusChanged += (s, a) => log.Write($"STEP advertising {name}: {a.Status} (error {a.Error})");
        _providers.Add(p);
        _keepAlive.Add(p);
        _keepAlive.Add(p.Service);
        return p;
    }

    private Task<GattLocalCharacteristic?> AddCharacteristicAsync(GattServiceProvider p, string name, ushort uuid,
        GattCharacteristicProperties props, GattProtectionLevel read, GattProtectionLevel write, Func<byte[]>? value = null,
        Action<byte[], GattWriteOption>? onWrite = null)
        => AddCharacteristicAsync(p, name, Uuid(uuid), props, read, write, value, onWrite);

    private async Task<GattLocalCharacteristic?> AddCharacteristicAsync(GattServiceProvider p, string name, Guid uuid,
        GattCharacteristicProperties props, GattProtectionLevel read, GattProtectionLevel write, Func<byte[]>? value = null,
        Action<byte[], GattWriteOption>? onWrite = null)
    {
        string svc = p.Service.Uuid.ToString()[..8];
        name = $"{svc}/{name}";
        var r = await p.Service.CreateCharacteristicAsync(uuid, new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = props,
            ReadProtectionLevel = read,
            WriteProtectionLevel = write,
        });
        log.Write($"  characteristic {name} ({uuid}) [{props}, read {read}, write {write}]: {r.Error}");
        if (r.Error != BluetoothError.Success) return null;
        var c = r.Characteristic;
        _keepAlive.Add(c);
        if (value != null)
        {
            c.ReadRequested += async (s, a) =>
            {
                log.Write($"EVENT read {name} (thread {Environment.CurrentManagedThreadId})");
                var d = a.GetDeferral();
                try
                {
                    Track(a.Session, $"read {name}");
                    var req = await a.GetRequestAsync();
                    if (req == null) { log.Write($"  read {name}: request null (access denied by the stack?)"); return; }
                    byte[] v = value();
                    int offset = (int)Math.Min(req.Offset, (uint)v.Length);
                    req.RespondWithValue(Buf(v[offset..]));
                    log.Write($"STEP read {name} offset {req.Offset} → {v.Length - offset} bytes");
                }
                catch (Exception e) { log.Write($"  read {name} error: {e}"); }
                finally { d.Complete(); }
            };
        }
        if (onWrite != null)
        {
            c.WriteRequested += async (s, a) =>
            {
                log.Write($"EVENT write {name}");
                var d = a.GetDeferral();
                try
                {
                    Track(a.Session, $"write {name}");
                    var req = await a.GetRequestAsync();
                    if (req == null) { log.Write($"  write {name}: request null"); return; }
                    CryptographicBuffer.CopyToByteArray(req.Value, out byte[] bytes);
                    log.Write($"STEP write {name} ({req.Option}): {Convert.ToHexString(bytes ?? [])}");
                    onWrite(bytes ?? [], req.Option);
                    if (req.Option == GattWriteOption.WriteWithResponse) req.Respond();
                }
                catch (Exception e) { log.Write($"  write {name} error: {e}"); }
                finally { d.Complete(); }
            };
        }
        c.SubscribedClientsChanged += (s, a) =>
        {
            log.Write($"EVENT subscribers changed {name}");
            var list = s.SubscribedClients;
            log.Write($"STEP subscribe {name}: {list.Count} client(s)" +
                      string.Concat(list.Select(x => $" [{Short(x.Session.DeviceId.Id)} maxNotify {x.MaxNotificationSize}]")));
            foreach (var x in list) Track(x.Session, $"subscribe {name}");
        };
        return c;
    }

    private async Task AddReportReferenceAsync(GattLocalCharacteristic? c, string name, byte id, byte type)
    {
        if (c == null) return;
        var r = await c.CreateDescriptorAsync(Uuid(0x2908), new GattLocalDescriptorParameters
        {
            StaticValue = Buf(id, type),
            ReadProtectionLevel = GattProtectionLevel.EncryptionRequired,
        });
        if (r.Descriptor != null) _keepAlive.Add(r.Descriptor);
        log.Write($"  descriptor Report Reference for {name} (id {id}, type {type}): {r.Error}");
    }

    private async Task CreateControlServiceAsync()
    {
        var p = await CreateProviderAsync("Control", ControlService);
        if (p == null) return;
        var RN = GattCharacteristicProperties.Read | GattCharacteristicProperties.Notify;
        _ctlPlain = await AddCharacteristicAsync(p, "plain", ControlPlain, RN, GattProtectionLevel.Plain, GattProtectionLevel.Plain,
            () => Encoding.ASCII.GetBytes("PhoneEdge-OK"));
        _ctlEncrypted = await AddCharacteristicAsync(p, "encrypted", ControlEncrypted, RN, GattProtectionLevel.EncryptionRequired,
            GattProtectionLevel.EncryptionRequired, () => Encoding.ASCII.GetBytes("PhoneEdge-ENC"));
        Advertise(p, "Control", discoverable: true, marker: false);
    }

    private async Task ControlNotifyLoopAsync()
    {
        int n = 0;
        while (!_quit)
        {
            for (int i = 0; i < 30 && !_quit; i++) await Task.Delay(100);
            n++;
            foreach (var (c, label) in new[] { (_ctlPlain, "control plain"), (_ctlEncrypted, "control encrypted") })
                await NotifyAsync(c, label, Encoding.ASCII.GetBytes($"tick {n}"), verbose: n % 10 == 1);
        }
    }

    /// <summary>Link-level view: any LE device that becomes connected to this PC, independent of GATT requests.</summary>
    private void StartLinkWatcher()
    {
        try
        {
            var w = DeviceInformation.CreateWatcher(BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected));
            w.Added += (s, d) => log.Write($"LINK LE connected: '{d.Name}' {Short(d.Id)}");
            w.Removed += (s, d) => log.Write($"LINK LE disconnected: {Short(d.Id)}");
            w.Updated += (s, d) => { };
            w.EnumerationCompleted += (s, _) => log.Write("LINK watcher ready (lines above this are devices already connected)");
            w.Start();
            _keepAlive.Add(w);
        }
        catch (Exception e) { log.Write("LINK watcher failed: " + e.Message); }
    }

    private async Task CreateHidServiceAsync()
    {
        var p = await CreateProviderAsync("HID", 0x1812);
        if (p == null) return;
        const GattProtectionLevel enc = GattProtectionLevel.EncryptionRequired;
        var R = GattCharacteristicProperties.Read;
        var N = GattCharacteristicProperties.Notify;
        var W = GattCharacteristicProperties.Write;
        var WNR = GattCharacteristicProperties.WriteWithoutResponse;
        byte[] map = ReportMap();
        log.Write($"  report map {map.Length} bytes: {Convert.ToHexString(map)}");

        // bcdHID 1.11, country 0, flags: RemoteWake | NormallyConnectable
        await AddCharacteristicAsync(p, "HID Information", 0x2A4A, R, enc, enc, () => [0x11, 0x01, 0x00, 0x03]);
        await AddCharacteristicAsync(p, "Report Map", 0x2A4B, R, enc, enc, () => map);
        await AddCharacteristicAsync(p, "HID Control Point", 0x2A4C, WNR, enc, enc, null,
            (v, _) => log.Write(v.Length > 0 && v[0] == 0 ? "  host: Suspend" : "  host: Exit Suspend"));
        byte protocolMode = 1; // Report protocol
        await AddCharacteristicAsync(p, "Protocol Mode", 0x2A4E, R | WNR, enc, enc, () => [protocolMode], (v, _) => { if (v.Length > 0) protocolMode = v[0]; });

        _keyboardInput = await AddCharacteristicAsync(p, "Report keyboard input", 0x2A4D, R | N, enc, enc, () => new byte[HidReports.KeyboardReportSize]);
        await AddReportReferenceAsync(_keyboardInput, "keyboard input", KeyboardReportId, 1);
        var leds = await AddCharacteristicAsync(p, "Report keyboard LED output", 0x2A4D, R | W | WNR, enc, enc, () => [0],
            (v, _) => log.Write($"  keyboard LEDs: 0x{(v.Length > 0 ? v[0] : 0):X2}"));
        await AddReportReferenceAsync(leds, "keyboard LED output", KeyboardReportId, 2);
        _mouseInput = await AddCharacteristicAsync(p, "Report mouse input", 0x2A4D, R | N, enc, enc, () => new byte[HidReports.MouseReportSize]);
        await AddReportReferenceAsync(_mouseInput, "mouse input", MouseReportId, 1);

        Advertise(p, "HID", discoverable: true, marker: true);
    }

    private async Task CreateBatteryServiceAsync()
    {
        var p = await CreateProviderAsync("Battery", 0x180F);
        if (p == null) return;
        await AddCharacteristicAsync(p, "Battery Level", 0x2A19, GattCharacteristicProperties.Read | GattCharacteristicProperties.Notify,
            GattProtectionLevel.Plain, GattProtectionLevel.Plain, () => [100]);
        Advertise(p, "Battery", discoverable: false);
    }

    private async Task CreateDeviceInfoServiceAsync()
    {
        // Microsoft's GATT server guide lists Device Information as reserved by the system; logged either way.
        var p = await CreateProviderAsync("Device Information", 0x180A);
        if (p == null) return;
        // PnP ID: source 2 (USB-IF), vendor 0x0000, product 0x0001, version 0x0001
        await AddCharacteristicAsync(p, "PnP ID", 0x2A50, GattCharacteristicProperties.Read, GattProtectionLevel.Plain, GattProtectionLevel.Plain,
            () => [0x02, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00]);
        await AddCharacteristicAsync(p, "Manufacturer Name", 0x2A29, GattCharacteristicProperties.Read, GattProtectionLevel.Plain, GattProtectionLevel.Plain,
            () => Encoding.UTF8.GetBytes("PhoneEdge"));
        Advertise(p, "Device Information", discoverable: false);
    }

    private void Guide()
    {
        string[] lines =
        [
            "",
            "================ 3차 대조 실험: 폰에서 할 일 ================",
            "1. nRF Connect에서 SCAN → Service Data 0x1812 = 'PhoneEdge'인 PC 항목 → CONNECT",
            "2. CLIENT 탭에서 'Unknown Service 6c0e2a40-...'(대조 서비스)를 펼치고:",
            "   a. 6c0e2a41 특성(암호화 없음) 읽기 버튼 → 값이 'PhoneEdge-OK'로 보여야 함",
            "   b. 6c0e2a42 특성(암호화 필요) 읽기 버튼 → 'PhoneEdge-ENC'",
            "3. Human Interface Device(0x1812) → Report Map 읽기 버튼",
            "4. 각 특성의 알림 구독 버튼(여러 개 화살표 아이콘) 누르기: 대조 2개, HID Report 2개",
            "5. 값이 보이는 화면을 캡처한 뒤, 이 창에서 Enter로 종료",
            "이 창에 'EVENT ...' 줄이 생기는지가 핵심입니다.",
            "==============================================================",
            "",
        ];
        foreach (var l in lines) log.Write(l);
    }

    private void Advertise(GattServiceProvider p, string name, bool discoverable, bool marker = false)
    {
        try
        {
            var parameters = new GattServiceProviderAdvertisingParameters { IsConnectable = true, IsDiscoverable = discoverable };
            if (marker && Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent(
                    "Windows.Devices.Bluetooth.GenericAttributeProfile.GattServiceProviderAdvertisingParameters", "ServiceData"))
            {
                parameters.ServiceData = Buf(Marker);
                log.Write($"  {name} advertisement carries Service Data 'PhoneEdge' ({Convert.ToHexString(Marker)})");
            }
            else if (marker) log.Write($"  {name}: ServiceData not available on this Windows build");
            p.StartAdvertising(parameters);
            log.Write($"STEP advertising {name} requested (connectable, discoverable={discoverable}), status now {p.AdvertisementStatus}");
        }
        catch (Exception e)
        {
            log.Write($"STEP advertising {name} FAILED: {e.GetType().Name} 0x{e.HResult:X8} {e.Message}");
        }
    }

    // ---------------- central (phone) tracking and pairing ----------------

    private static string Short(string deviceId) => deviceId.Length > 12 ? "…" + deviceId[^12..] : deviceId;

    private void Track(GattSession session, string reason)
    {
        string id = session.DeviceId.Id;
        lock (_sessions)
        {
            if (_sessions.ContainsKey(id)) return;
            _sessions[id] = new SessionInfo { Session = session };
        }
        log.Write($"STEP central connected ({reason}): {Short(id)}, session {session.SessionStatus}, maxPdu {session.MaxPduSize}");
        session.SessionStatusChanged += (s, a) => log.Write($"STEP session {Short(id)}: {a.Status} (error {a.Error})");
        session.MaxPduSizeChanged += (s, _) => log.Write($"  session {Short(id)} maxPdu {s.MaxPduSize}");
        _ = Task.Run(() => WatchDeviceAsync(id));
    }

    private async Task WatchDeviceAsync(string id)
    {
        try
        {
            var dev = await BluetoothLEDevice.FromIdAsync(id);
            if (dev == null) { log.Write($"  device {Short(id)}: FromIdAsync returned null"); return; }
            lock (_sessions) _sessions[id].Device = dev;
            var pairing = dev.DeviceInformation.Pairing;
            log.Write($"STEP device {Short(id)} name '{dev.Name}', connection {dev.ConnectionStatus}, paired {pairing.IsPaired}, " +
                      $"protection {pairing.ProtectionLevel}, canPair {pairing.CanPair}");
            dev.ConnectionStatusChanged += (s, _) =>
                log.Write($"STEP device {Short(id)} connection {s.ConnectionStatus}, paired {s.DeviceInformation.Pairing.IsPaired}");

            // The phone normally starts bonding itself. If nothing is bonded after a while, try pairing from this side.
            await Task.Delay(TimeSpan.FromSeconds(10));
            if (_quit || dev.DeviceInformation.Pairing.IsPaired) return;
            lock (_sessions) { if (_sessions[id].PairAttempted) return; _sessions[id].PairAttempted = true; }
            var custom = dev.DeviceInformation.Pairing.Custom;
            custom.PairingRequested += (s, a) =>
            {
                log.Write($"STEP pairing request from Windows: kind {a.PairingKind}, pin '{a.Pin}' — accepting (compare the number on the phone if shown)");
                a.Accept();
            };
            log.Write($"STEP pairing not done after 10 s; trying PairAsync from the PC side");
            var r = await custom.PairAsync(
                DevicePairingKinds.ConfirmOnly | DevicePairingKinds.ConfirmPinMatch | DevicePairingKinds.DisplayPin,
                DevicePairingProtectionLevel.Encryption);
            log.Write($"STEP pairing result: {r.Status}, protection {r.ProtectionLevelUsed}");
        }
        catch (Exception e)
        {
            log.Write($"  device {Short(id)} watch error: {e.GetType().Name} 0x{e.HResult:X8} {e.Message}");
        }
    }

    // ---------------- demo ----------------

    private async Task<bool> NotifyAsync(GattLocalCharacteristic? c, string name, byte[] report, bool verbose)
    {
        if (c == null || c.SubscribedClients.Count == 0) return false;
        var results = await c.NotifyValueAsync(Buf(report));
        bool ok = results.Count > 0 && results.All(r => r.Status == GattCommunicationStatus.Success);
        if (verbose || !ok)
            log.Write($"STEP send {name} {Convert.ToHexString(report)}: " +
                      string.Join(", ", results.Select(r => $"{r.Status}{(r.ProtocolError is byte pe ? $" (ATT error 0x{pe:X2})" : "")}")));
        return ok;
    }

    private async Task DemoLoopAsync()
    {
        int round = 0;
        while (!_quit)
        {
            bool mouseReady = _mouseInput?.SubscribedClients.Count > 0;
            bool keyboardReady = _keyboardInput?.SubscribedClients.Count > 0;
            if (!mouseReady && !keyboardReady) { await Task.Delay(500); continue; }

            round++;
            log.Write($"DEMO round {round}: square with the mouse, then 'hello' (mouse subscribed {mouseReady}, keyboard subscribed {keyboardReady})");
            int sent = 0, failed = 0;
            if (mouseReady)
            {
                foreach (var (dx, dy) in new[] { (15, 0), (0, 15), (-15, 0), (0, -15) })
                    for (int i = 0; i < 20 && !_quit; i++)
                    {
                        if (await NotifyAsync(_mouseInput, "mouse", HidReports.Mouse(dx, dy, PhoneMouseButtons.None, 0), verbose: sent == 0 && round == 1)) sent++; else failed++;
                        await Task.Delay(15);
                    }
            }
            if (keyboardReady)
            {
                foreach (byte k in new byte[] { 0x0B, 0x08, 0x0F, 0x0F, 0x12 }) // h e l l o
                {
                    if (await NotifyAsync(_keyboardInput, "key down", HidReports.Keyboard(0, [k]), verbose: round == 1)) sent++; else failed++;
                    await Task.Delay(40);
                    if (await NotifyAsync(_keyboardInput, "key up", HidReports.Keyboard(0, []), verbose: false)) sent++; else failed++;
                    await Task.Delay(40);
                }
            }
            log.Write($"DEMO round {round} done: {sent} reports sent, {failed} failed. Check the phone: did the cursor draw a square and did 'hello' appear?");
            for (int i = 0; i < 40 && !_quit; i++) await Task.Delay(100);
        }
    }

    private async Task StatusLoopAsync()
    {
        while (!_quit)
        {
            for (int i = 0; i < 300 && !_quit; i++) await Task.Delay(100);
            if (_quit) break;
            string adv = string.Join(", ", _providers.Select(p => $"{p.Service.Uuid.ToString()[4..8]}={p.AdvertisementStatus}"));
            int subs = (_mouseInput?.SubscribedClients.Count ?? 0) + (_keyboardInput?.SubscribedClients.Count ?? 0);
            string devs;
            lock (_sessions) devs = string.Join(", ", _sessions.Values.Select(s =>
                s.Device == null ? "?" : $"{s.Device.Name}:{s.Device.ConnectionStatus}/paired={s.Device.DeviceInformation.Pairing.IsPaired}"));
            log.Write($"status: advertising [{adv}], input subscriptions {subs}, centrals [{devs}]");
        }
    }
}
