using PhoneEdge.Contracts;

namespace PhoneEdge.App;

/// <summary>IPhoneLink 를 만드는 유일한 조립 지점. 실제 구현(AdbPhoneLink)은 여기에 연결한다.</summary>
internal static class PhoneLinkFactory
{
    public static IPhoneLink Create(string[] args, Action<string> log)
    {
        if (args.Contains("--fake")) return new FakePhoneLink(log);

        var tools = Path.Combine(AppContext.BaseDirectory, "tools");
        return new PhoneEdge.Phone.AdbPhoneLink(
            Path.Combine(tools, "adb.exe"),
            Path.Combine(tools, "scrcpy-server"),
            new PhoneEdge.Phone.AdbPhoneLinkOptions { Log = log });
    }
}
