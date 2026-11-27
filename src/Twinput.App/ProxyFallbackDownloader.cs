using System.Net;
using System.Net.Http;
using Twinput.App.Core;
using Velopack.Sources;

namespace Twinput.App;

/// <summary>
/// 업데이트 다운로더. 기본은 Windows 프록시 설정을 따르고, 프록시에 닿지 못하면 같은 요청을 직접 연결로 다시 보낸다.
/// 한 번 직접 연결로 성공하면 이 실행 동안은 직접 연결을 먼저 쓴다 (실패하면 다시 프록시를 시도).
/// </summary>
internal sealed class ProxyFallbackDownloader : IFileDownloader
{
    private readonly HttpClientFileDownloader _system = new();
    private readonly HttpClientFileDownloader _direct = new DirectDownloader();
    private volatile bool _preferDirect;

    private sealed class DirectDownloader : HttpClientFileDownloader
    {
        protected override HttpClientHandler CreateHttpClientHandler()
        {
            var h = base.CreateHttpClientHandler();
            h.UseProxy = false;
            return h;
        }
    }

    private static bool ProxyInUse(string url)
    {
        try
        {
            var uri = new Uri(url);
            var proxy = HttpClient.DefaultProxy;
            return !proxy.IsBypassed(uri) && proxy.GetProxy(uri) is { } p && p != uri;
        }
        catch { return false; }
    }

    private async Task<T> Run<T>(string url, Func<IFileDownloader, Task<T>> call)
    {
        var first = _preferDirect ? _direct : _system;
        var second = _preferDirect ? _system : _direct;
        try
        {
            return await call(first).ConfigureAwait(false);
        }
        catch (Exception ex) when (_preferDirect || ProxyFallback.ShouldRetryDirect(ex, ProxyInUse(url)))
        {
            Logger.Info($"업데이트 연결 실패({(_preferDirect ? "직접" : "프록시")}): {ex.Message} → {(_preferDirect ? "프록시" : "직접 연결")}로 다시 시도");
            var r = await call(second).ConfigureAwait(false);
            _preferDirect = !_preferDirect;
            return r;
        }
    }

    public Task DownloadFile(string url, string targetFile, Action<int> progress, IDictionary<string, string>? headers = null, double timeout = 30, CancellationToken cancelToken = default)
        => Run(url, async d => { await d.DownloadFile(url, targetFile, progress, headers, timeout, cancelToken).ConfigureAwait(false); return true; });

    public Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers = null, double timeout = 30)
        => Run(url, d => d.DownloadBytes(url, headers, timeout));

    public Task<string> DownloadString(string url, IDictionary<string, string>? headers = null, double timeout = 30)
        => Run(url, d => d.DownloadString(url, headers, timeout));
}
