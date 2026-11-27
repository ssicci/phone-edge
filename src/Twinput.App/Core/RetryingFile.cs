namespace Twinput.App.Core;

/// <summary>
/// 잠깐 잠겨 있을 수 있는 파일 작업을 다시 시도한다.
/// 설치·업데이트 직후 Velopack 이 앱을 다시 띄우는 순간에는 데이터 폴더의 파일을 바로 쓰지 못하는 경우가 있었다 (v0.2.0).
/// 실패 이유는 <c>errors</c> 에 모아 호출자가 로그에 남긴다.
/// </summary>
public static class RetryingFile
{
    /// <summary>쓰기용으로 연다 (다른 프로세스의 읽기·쓰기 공유 허용). 모두 실패하면 null.</summary>
    public static FileStream? Open(string path, FileMode mode, int attempts, TimeSpan delay, List<string> errors, Action<TimeSpan>? sleep = null)
    {
        FileStream? result = null;
        Try(() =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            result = new FileStream(path, mode, FileAccess.Write, FileShare.ReadWrite);
        }, attempts, delay, errors, sleep, path);
        return result;
    }

    /// <summary><paramref name="action"/> 이 예외 없이 끝날 때까지 최대 <paramref name="attempts"/> 번 실행한다.</summary>
    public static bool Try(Action action, int attempts, TimeSpan delay, List<string> errors, Action<TimeSpan>? sleep = null, string? what = null)
    {
        sleep ??= Thread.Sleep;
        for (int i = 1; i <= attempts; i++)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                errors.Add($"{what ?? "file"} #{i}: {ex.GetType().Name}: {ex.Message}");
                if (i < attempts) sleep(delay);
            }
        }
        return false;
    }
}
