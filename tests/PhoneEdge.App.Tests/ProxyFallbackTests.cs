using System.Net.Http;
using PhoneEdge.App.Core;
using Xunit;

namespace PhoneEdge.App.Tests;

/// <summary>
/// v0.5.3: 회사 프록시(수동 설정)가 켜진 노트북을 핫스팟에 연결하면 프록시가 연결을 거부해 업데이트 확인이 실패했다.
/// 같은 주소로 프록시 없이 직접 연결하면 성공했다. 프록시에 닿지 못한 경우에만 직접 연결로 다시 시도한다.
/// </summary>
public class ProxyFallbackTests
{
    [Theory]
    [InlineData(HttpRequestError.ConnectionError)]     // 프록시가 연결을 거부함 (실측: 10061)
    [InlineData(HttpRequestError.ProxyTunnelError)]    // 프록시가 터널을 열지 못함
    [InlineData(HttpRequestError.NameResolutionError)] // 프록시 이름을 찾지 못함 (사내 DNS 밖)
    public void RetriesDirectWhenTheProxyCannotBeReached(HttpRequestError error)
        => Assert.True(ProxyFallback.ShouldRetryDirect(new HttpRequestException(error, "x"), proxyInUse: true));

    [Fact]
    public void DoesNotRetryWithoutAProxy()
        => Assert.False(ProxyFallback.ShouldRetryDirect(new HttpRequestException(HttpRequestError.ConnectionError, "x"), proxyInUse: false));

    [Fact]
    public void DoesNotRetryOnServerAnswers()
    {
        // 서버가 응답한 오류(404 등)는 프록시 문제가 아니므로 그대로 실패로 둔다
        Assert.False(ProxyFallback.ShouldRetryDirect(new HttpRequestException(HttpRequestError.Unknown, "404", null, System.Net.HttpStatusCode.NotFound), proxyInUse: true));
        Assert.False(ProxyFallback.ShouldRetryDirect(new InvalidOperationException(), proxyInUse: true));
    }
}
