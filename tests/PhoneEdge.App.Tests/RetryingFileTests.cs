using PhoneEdge.App.Core;
using Xunit;

namespace PhoneEdge.App.Tests;

/// <summary>
/// 설치·업데이트 직후 Velopack 이 다시 띄운 앱이 데이터 폴더에 쓰지 못하던 문제 (v0.2.0).
/// 시작 순간 파일이 잠겨 있어도 잠시 뒤 다시 시도해 열어야 하고, 실패 이유를 남겨야 한다.
/// </summary>
public class RetryingFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "PhoneEdgeTests", Guid.NewGuid().ToString("N"));
    private string PathIn(string name) => Path.Combine(_dir, name);

    public RetryingFileTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    [Fact]
    public void OpensAfterLockIsReleased()
    {
        var path = PathIn("app.log");
        var errors = new List<string>();
        var holder = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        int sleeps = 0;
        // 두 번째 대기에서 잠금이 풀린다 (설치 프로그램이 파일을 놓는 순간)
        using var fs = RetryingFile.Open(path, FileMode.Create, 10, TimeSpan.Zero, errors, _ => { if (++sleeps == 2) holder.Dispose(); });
        Assert.NotNull(fs);
        Assert.Equal(2, errors.Count);
        Assert.Contains("IOException", errors[0]);
    }

    [Fact]
    public void GivesUpAfterAttemptsAndRecordsEachFailure()
    {
        var path = PathIn("locked.log");
        var errors = new List<string>();
        using var holder = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        var fs = RetryingFile.Open(path, FileMode.Create, 3, TimeSpan.Zero, errors, _ => { });
        Assert.Null(fs);
        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void TryRunsActionUntilItSucceeds()
    {
        var errors = new List<string>();
        int calls = 0;
        bool ok = RetryingFile.Try(() => { if (++calls < 3) throw new IOException("busy"); }, 5, TimeSpan.Zero, errors, _ => { });
        Assert.True(ok);
        Assert.Equal(3, calls);
        Assert.Equal(2, errors.Count);
    }
}
