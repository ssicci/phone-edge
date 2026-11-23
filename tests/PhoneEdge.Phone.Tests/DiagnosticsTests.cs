using PhoneEdge.Contracts;
using PhoneEdge.Phone.Diagnostics;

namespace PhoneEdge.Phone.Tests;

public class DiagnosticsTests
{
    private const string Header = "List of devices attached\r\n";

    // 2026-11-23 실측: 폴드7 USB 디버깅 꺼짐 상태의 Windows USB 장치 목록 (일부)
    private static readonly string[] UsbWithFoldDebugOff =
    [
        @"USB\VID_30C9&PID_00CE&MI_00\6&38A98B4&1&0000",            // LG 카메라 인터페이스
        @"USB\VID_04E8&PID_6860&MS_COMP_MTP&SAMSUNG_ANDROID\6&3C463CD&4&0000",
        @"USB\VID_04E8&PID_6860&MODEM\6&3C463CD&4&0001",
        @"USB\VID_04E8&PID_6860\R3XX00000AA",                        // 폴드7 (부모)
        @"USB\VID_30C9&PID_00CE\01.00.00",
        @"USB\VID_8087&PID_0033\5&2D5A6FE5&0&10",                     // 인텔 블루투스
    ];

    private static readonly string[] UsbWithoutPhone =
    [
        @"USB\VID_30C9&PID_00CE\01.00.00",
        @"USB\VID_8087&PID_0033\5&2D5A6FE5&0&10",
        @"USB\VID_04E8&PID_61F5\S6XNNS0T000000",                       // 삼성 외장 SSD (폰 아님)
    ];

    private static DeviceVerdict Classify(string output, params string[] usb)
        => LinkDiagnostics.ClassifyDevices(new AdbDevicesResult(0, output, null), usb);

    [Fact]
    public void One_device_is_ready_to_connect()
    {
        var v = Classify(Header + "R3XX00000AA            device usb:1-2 product:q7qksx model:SM_F966N device:q7q transport_id:1\r\n");
        Assert.Equal((PhoneLinkProblem.None, "R3XX00000AA"), (v.Problem, v.Serial));
    }

    [Fact]
    public void Nothing_in_adb_and_phone_on_usb_is_debugging_off()
    {
        var v = Classify(Header, UsbWithFoldDebugOff);
        Assert.Equal(PhoneLinkProblem.DebuggingOff, v.Problem);
        Assert.Contains(@"USB\VID_04E8&PID_6860\R3XX00000AA", v.Detail);
        Assert.DoesNotContain("MODEM", v.Detail); // 한 폰은 한 번만
    }

    [Fact]
    public void Nothing_in_adb_and_no_phone_on_usb_is_no_device()
        => Assert.Equal(PhoneLinkProblem.NoDevice, Classify(Header, UsbWithoutPhone).Problem);

    [Fact]
    public void Unauthorized()
        => Assert.Equal(PhoneLinkProblem.Unauthorized, Classify(Header + "R3XX00000AA            unauthorized usb:1-2 transport_id:3\r\n").Problem);

    [Fact]
    public void Offline()
        => Assert.Equal(PhoneLinkProblem.Offline, Classify(Header + "R3XX00000AA            offline usb:1-2 transport_id:4\r\n").Problem);

    [Fact]
    public void No_permissions_counts_as_offline()
        => Assert.Equal(PhoneLinkProblem.Offline, Classify(Header + "R3XX00000AA            no permissions; see [http://developer.android.com/tools/device.html]\r\n").Problem);

    [Fact]
    public void Authorizing_is_transient_not_a_problem()
    {
        var v = Classify(Header + "R3XX00000AA            authorizing usb:1-2 transport_id:5\r\n");
        Assert.Equal((PhoneLinkProblem.None, (string?)null), (v.Problem, v.Serial));
    }

    [Fact]
    public void Two_usable_devices_is_multiple()
        => Assert.Equal(PhoneLinkProblem.MultipleDevices,
            Classify(Header + "R3XX00000AA device usb:1-2\r\nemulator-5554 device product:sdk\r\n").Problem);

    [Fact]
    public void One_usable_plus_one_unauthorized_still_connects()
        => Assert.Equal("R3XX00000AA", Classify(Header + "R3XX00000AA device usb:1-2\r\nOTHER unauthorized usb:1-3\r\n").Serial);

    [Fact]
    public void Adb_exception_is_adb_failed()
        => Assert.Equal(PhoneLinkProblem.AdbFailed,
            LinkDiagnostics.ClassifyDevices(new AdbDevicesResult(null, "", "Win32Exception: 액세스가 거부되었습니다"), []).Problem);

    [Fact]
    public void Adb_daemon_error_is_adb_failed_even_with_exit_zero()
    {
        const string output = "* daemon not running; starting now at tcp:5037\n* failed to start daemon\nerror: cannot connect to daemon\n";
        Assert.Equal(PhoneLinkProblem.AdbFailed, LinkDiagnostics.ClassifyDevices(new AdbDevicesResult(0, output, null), UsbWithFoldDebugOff).Problem);
        Assert.Equal(PhoneLinkProblem.AdbFailed, LinkDiagnostics.ClassifyDevices(new AdbDevicesResult(1, output, null), []).Problem);
    }

    [Fact]
    public void Foreign_listener_on_adb_port_is_adb_failed()
    {
        // 실측: 5037 포트를 다른 프로그램이 잡고 엉뚱한 응답을 보낼 때
        const string output = "adb.exe: failed to check server version: protocol fault (status 58 58 58 58?!)\n";
        Assert.Equal(PhoneLinkProblem.AdbFailed, LinkDiagnostics.ClassifyDevices(new AdbDevicesResult(1, output, null), []).Problem);
    }

    [Fact]
    public void Daemon_started_banner_is_not_a_failure()
        => Assert.Equal(PhoneLinkProblem.NoDevice,
            Classify("* daemon not running; starting now at tcp:5037\r\n* daemon started successfully\r\n" + Header).Problem);

    [Theory]
    [InlineData(@"USB\VID_04E8&PID_6860\R3XX00000AA", true)]
    [InlineData(@"USB\VID_04E8&PID_6860&MODEM\6&3C463CD&4&0001", false)]
    [InlineData(@"USB\VID_04E8&PID_6860&MI_01\6&3C463CD&4&0001", false)]
    [InlineData(@"USB\VID_04E8&PID_61F5\S6XNNS0T000000", false)]
    [InlineData(@"USB\VID_18D1&PID_4EE7\1A2B3C", true)]
    [InlineData(@"USB\VID_8087&PID_0033\5&2D5A6FE5&0&10", false)]
    [InlineData(@"HID\VID_04E8&PID_6860\x", false)]
    public void IsAndroidPhoneUsbDevice(string id, bool expected)
        => Assert.Equal(expected, LinkDiagnostics.IsAndroidPhoneUsbDevice(id));

    [Fact]
    public void HasVirtualInputDevices_needs_both_names_as_device_lines()
    {
        const string both = "  Device 18: PhoneEdge Keyboard\n    EventHub Devices: [ 23 ]\n  Device 19: PhoneEdge Mouse\n";
        Assert.True(LinkDiagnostics.HasVirtualInputDevices(both, "PhoneEdge Mouse", "PhoneEdge Keyboard"));
        Assert.False(LinkDiagnostics.HasVirtualInputDevices("  Device 18: PhoneEdge Keyboard\n    24: PhoneEdge Mouse\n", "PhoneEdge Mouse", "PhoneEdge Keyboard"));
    }

}
