using Twinput.App.Core;
using Xunit;

namespace Twinput.App.Tests;

public class UpdatePolicyTests
{
    [Theory]
    [InlineData("0.1.0", "0.1.1", -1)]
    [InlineData("0.1.1", "0.1.0", 1)]
    [InlineData("0.9.0", "0.10.0", -1)]   // 숫자로 비교 (문자열 비교가 아님)
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0.0-beta.1", "1.0.0", -1)]
    [InlineData("v2.0.0", "1.9.9", 1)]
    public void VersionsCompare(string a, string b, int expectedSign)
    {
        Assert.True(AppVersion.TryParse(a, out var va));
        Assert.True(AppVersion.TryParse(b, out var vb));
        Assert.Equal(expectedSign, Math.Sign(va.CompareTo(vb)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("abc")]
    [InlineData(null)]
    public void InvalidVersionsAreRejected(string? text) => Assert.False(AppVersion.TryParse(text, out _));

    [Fact]
    public void OffersOnlyNewerVersions()
    {
        Assert.True(UpdatePolicy.ShouldOffer("0.1.0", "0.1.1", null, false));
        Assert.False(UpdatePolicy.ShouldOffer("0.1.1", "0.1.1", null, true));
        Assert.False(UpdatePolicy.ShouldOffer("0.2.0", "0.1.1", null, true));
        Assert.False(UpdatePolicy.ShouldOffer("0.1.0", "garbage", null, true));
    }

    [Fact]
    public void AutomaticCheckHonorsSkippedVersionButManualDoesNot()
    {
        Assert.False(UpdatePolicy.ShouldOffer("0.1.0", "0.1.1", "0.1.1", manual: false));
        Assert.True(UpdatePolicy.ShouldOffer("0.1.0", "0.1.1", "0.1.1", manual: true));
        // 더 새로운 버전이 나오면 건너뛰기는 풀린다
        Assert.True(UpdatePolicy.ShouldOffer("0.1.0", "0.1.2", "0.1.1", manual: false));
    }

    [Fact]
    public void ReleaseNotesAreFlattenedToPlainText()
    {
        string md = "## [0.1.1] - 2026-01-01\r\n\r\n### 추가\r\n- **빠른** 연결 `adb`\r\n- [문서](http://x) 보기\r\n<!-- 숨김 -->\r\n\r\n\r\n\r\n끝";
        string text = ReleaseNotes.Clean(md).Replace("\r\n", "\n");
        Assert.Equal("[0.1.1] - 2026-01-01\n\n추가\n• 빠른 연결 adb\n• 문서 보기\n\n끝", text);
    }

    [Fact]
    public void BilingualNotesKeepBothLanguagesAndShowHorizontalRule()
    {
        string text = ReleaseNotes.Clean("First release.\n\n---\n\n첫 릴리스.\n\n### 추가\n- 기능").Replace("\r\n", "\n");
        Assert.Equal("First release.\n\n────────────────────\n\n첫 릴리스.\n\n추가\n• 기능", text);
    }

    [Fact]
    public void EmptyAndHugeNotesAreHandled()
    {
        Assert.Contains("기록되지", ReleaseNotes.Clean("  ", System.Globalization.CultureInfo.GetCultureInfo("ko")));
        Assert.Contains("No release notes", ReleaseNotes.Clean(null, System.Globalization.CultureInfo.GetCultureInfo("en")));
        Assert.True(ReleaseNotes.Clean(new string('가', 10000)).Length <= ReleaseNotes.MaxLength + 3);
    }

    [Fact]
    public void SettingsStorePersistsSkippedVersionAndSurvivesCorruption()
    {
        string dir = Path.Combine(Path.GetTempPath(), "twinput-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "settings.json");
        try
        {
            var store = new SettingsStore(path);
            Assert.Null(store.SkippedVersion);
            store.SkippedVersion = "0.1.1";
            Assert.Equal("0.1.1", new SettingsStore(path).SkippedVersion);
            File.WriteAllText(path, "{ not json");
            Assert.Null(new SettingsStore(path).SkippedVersion);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
