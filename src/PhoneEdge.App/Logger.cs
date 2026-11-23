using System.Collections.Concurrent;
using System.Text;

namespace PhoneEdge.App;

/// <summary>훅 스레드를 막지 않는 비동기 로그. 파일(%LOCALAPPDATA%\PhoneEdge\phoneedge.log)과 콘솔에 쓴다.</summary>
internal static class Logger
{
    private static readonly BlockingCollection<string> Queue = new(8192);
    public static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhoneEdge", "phoneedge.log");

    static Logger()
    {
        var t = new Thread(Pump) { IsBackground = true, Name = "PhoneEdge.Log" };
        t.Start();
    }

    public static void Info(string message) => Queue.TryAdd($"{DateTime.Now:HH:mm:ss.fff} {message}");

    public static void Flush()
    {
        for (int i = 0; i < 50 && Queue.Count > 0; i++) Thread.Sleep(20);
    }

    private static void Pump()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            using var file = new StreamWriter(new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };
            foreach (var line in Queue.GetConsumingEnumerable())
            {
                file.WriteLine(line);
                try { Console.WriteLine(line); } catch { }
            }
        }
        catch { /* 로그 실패가 앱을 죽이면 안 된다 */ }
    }
}
