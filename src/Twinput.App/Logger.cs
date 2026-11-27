using System.Collections.Concurrent;
using System.Text;
using Twinput.App.Core;

namespace Twinput.App;

/// <summary>앱 데이터 폴더. 설정·로그·알림 아이콘을 둔다 (Velopack 설치 폴더와 같다).</summary>
internal static class AppPaths
{
    public static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Twinput");
}

/// <summary>
/// 훅 스레드를 막지 않는 비동기 로그. 파일과 콘솔에 쓴다.
/// 시작 순간 로그 파일을 열 수 없으면 잠시 다시 시도하고, 그래도 안 되면 TEMP 에 쓴다.
/// 열기에 실패한 이유는 로그 맨 앞에 남긴다. 그동안 들어온 줄은 큐에 남아 있다가 함께 기록된다.
/// </summary>
internal static class Logger
{
    private static readonly BlockingCollection<string> Queue = new(8192);
    private static readonly string PrimaryPath = Path.Combine(AppPaths.DataDir, "twinput.log");
    private static readonly string FallbackPath = Path.Combine(Path.GetTempPath(), "Twinput", "twinput.log");
    private static volatile string _filePath = PrimaryPath;

    /// <summary>지금 실제로 쓰고 있는 로그 파일.</summary>
    public static string FilePath => _filePath;

    static Logger()
    {
        var t = new Thread(Pump) { IsBackground = true, Name = "Twinput.Log" };
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
            var errors = new List<string>();
            // 약 10초 동안 다시 시도한다 (설치·업데이트 직후 잠깐 잠기는 경우)
            var stream = RetryingFile.Open(PrimaryPath, FileMode.Create, 20, TimeSpan.FromMilliseconds(500), errors);
            if (stream == null)
            {
                stream = RetryingFile.Open(FallbackPath, FileMode.Create, 3, TimeSpan.FromMilliseconds(200), errors);
                if (stream != null) _filePath = FallbackPath;
            }

            using var file = stream == null ? null : new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            foreach (var e in errors) Write(file, $"{DateTime.Now:HH:mm:ss.fff} 로그 파일 열기 실패: {e}");
            foreach (var line in Queue.GetConsumingEnumerable()) Write(file, line);
        }
        catch { /* 로그 실패가 앱을 죽이면 안 된다 */ }
    }

    private static void Write(StreamWriter? file, string line)
    {
        try { file?.WriteLine(line); } catch { }
        try { Console.WriteLine(line); } catch { }
    }
}
