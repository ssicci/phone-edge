using System.Diagnostics;
using System.Text;

namespace Twinput.Phone.Adb;

/// <summary>adb.exe 실행 도우미.</summary>
internal sealed class AdbRunner(string adbPath, Action<string> log)
{
    public string AdbPath { get; } = adbPath;

    public async Task<(int Code, string Output)> RunAsync(IEnumerable<string> args, TimeSpan timeout, CancellationToken ct)
    {
        using var p = new Process { StartInfo = CreateStartInfo(args) };
        p.Start();
        var stdout = p.StandardOutput.ReadToEndAsync(ct);
        var stderr = p.StandardError.ReadToEndAsync(ct);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await p.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(p);
            throw new TimeoutException($"adb {string.Join(' ', args)} 시간 초과");
        }
        string output = await stdout.ConfigureAwait(false) + await stderr.ConfigureAwait(false);
        return (p.ExitCode, output);
    }

    /// <summary>오래 도는 adb 프로세스 (서버, 화면 감시). 출력 줄마다 onLine 호출.</summary>
    public Process StartLong(IEnumerable<string> args, Action<string> onLine)
    {
        var p = new Process { StartInfo = CreateStartInfo(args), EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) onLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) onLine(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return p;
    }

    public static void TryKill(Process? p)
    {
        if (p == null) return;
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { /* 이미 종료 */ }
    }

    private ProcessStartInfo CreateStartInfo(IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(AdbPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        return psi;
    }

    public void Log(string message) => log(message);
}
