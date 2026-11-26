using System.Net.Http;

namespace PhoneEdge.App.Core;

/// <summary>
/// Windows 에 설정된 프록시에 닿지 못할 때 직접 연결로 다시 시도할지 판단한다. 순수 로직.
/// 노트북을 회사 밖(핫스팟 등)으로 가져가면 수동으로 설정된 회사 프록시가 연결을 거부해 업데이트 확인이 실패했다 (v0.5.3).
/// 회사 안에서는 프록시가 그대로 쓰이고, 프록시에 닿지 못한 경우에만 직접 연결을 쓴다.
/// </summary>
public static class ProxyFallback
{
    public static bool ShouldRetryDirect(Exception ex, bool proxyInUse)
    {
        if (!proxyInUse) return false;
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is HttpRequestException h && h.StatusCode == null
                && h.HttpRequestError is HttpRequestError.ConnectionError or HttpRequestError.ProxyTunnelError or HttpRequestError.NameResolutionError)
                return true;
        }
        return false;
    }
}
