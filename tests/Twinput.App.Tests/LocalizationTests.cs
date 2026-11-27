using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Twinput.App.Core;
using Twinput.Contracts;
using Xunit;

namespace Twinput.App.Tests;

public class LocalizationTests
{
    private static readonly CultureInfo Ko = CultureInfo.GetCultureInfo("ko");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en");

    private static Dictionary<string, string> Load(CultureInfo culture)
    {
        // tryParents: false → 해당 언어의 리소스 파일만 읽는다 (영어는 기본 리소스 = 비교 대상이 되는 InvariantCulture).
        var rm = new ResourceManager("Twinput.App.Resources.Strings", typeof(L).Assembly);
        var set = rm.GetResourceSet(culture.Equals(En) ? CultureInfo.InvariantCulture : culture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException("resource set not found: " + culture);
        return set.Cast<System.Collections.DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!);
    }

    [Fact]
    public void EnglishAndKoreanHaveTheSameKeysAndNoEmptyValues()
    {
        var en = Load(En);
        var ko = Load(Ko);
        Assert.Empty(en.Keys.Except(ko.Keys));
        Assert.Empty(ko.Keys.Except(en.Keys));
        Assert.All(en, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), "empty English value: " + kv.Key));
        Assert.All(ko, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), "empty Korean value: " + kv.Key));
    }

    [Fact]
    public void PlaceholdersMatchBetweenLanguages()
    {
        var en = Load(En);
        var ko = Load(Ko);
        foreach (var key in en.Keys)
        {
            var a = Regex.Matches(en[key], @"\{\d+\}").Select(m => m.Value).OrderBy(x => x);
            var b = Regex.Matches(ko[key], @"\{\d+\}").Select(m => m.Value).OrderBy(x => x);
            Assert.True(a.SequenceEqual(b), "placeholder mismatch: " + key);
        }
    }

    /// <summary>코드에 적힌 리소스 키가 모두 실제로 있는지 (오타 방지).</summary>
    [Fact]
    public void EveryResourceKeyUsedInCodeExists()
    {
        var keys = Load(En).Keys.ToHashSet();
        string root = FindRepoRoot();
        var pattern = new Regex("\"((?:Menu|Tray|Guide|Toast|Update|Start|Common)_[A-Za-z]+)\"");
        int found = 0;
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src", "Twinput.App"), "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
        {
            foreach (Match m in pattern.Matches(File.ReadAllText(file)))
            {
                found++;
                string k = m.Groups[1].Value;   // GuidanceTable 은 접두사(Guide_NoDevice)만 적고 _Short 등을 붙인다
                Assert.True(keys.Contains(k) || keys.Contains(k + "_Short"), $"{Path.GetFileName(file)}: unknown key {k}");
            }
        }
        Assert.True(found > 20);
    }

    public static IEnumerable<object[]> Problems =>
        Enum.GetValues<PhoneLinkProblem>().Where(p => p != PhoneLinkProblem.None).Select(p => new object[] { p });

    [Theory, MemberData(nameof(Problems))]
    public void EveryProblemHasGuidanceInBothLanguages(PhoneLinkProblem p)
    {
        var ko = GuidanceTable.For(p, Ko)!;
        var en = GuidanceTable.For(p, En)!;
        foreach (var g in new[] { ko, en })
        {
            Assert.False(string.IsNullOrWhiteSpace(g.ShortCause));
            Assert.False(string.IsNullOrWhiteSpace(g.Cause));
            Assert.NotEmpty(g.Steps);
        }
        Assert.Equal(ko.Steps.Length, en.Steps.Length);
        Assert.Matches("[가-힣]", ko.Cause);
        Assert.DoesNotMatch("[가-힣]", en.Cause);
        Assert.DoesNotMatch("[가-힣]", string.Concat(en.Steps));
    }

    [Fact]
    public void TooltipFollowsTheRequestedLanguage()
    {
        var d = new PhoneLinkDiagnosis(PhoneLinkProblem.Unauthorized, PhoneLinkWarnings.None, null);
        Assert.Equal("Twinput - USB 디버깅 허용 필요", TrayStatus.Tooltip(TrayIconKind.Problem, d, Ko));
        Assert.Equal("Twinput - Allow USB debugging", TrayStatus.Tooltip(TrayIconKind.Problem, d, En));
        Assert.Equal("Twinput - Ready", TrayStatus.Tooltip(TrayIconKind.Ready, PhoneLinkDiagnosis.Healthy, En));
        Assert.All(Enum.GetValues<PhoneLinkProblem>(), p => Assert.InRange(TrayStatus.Tooltip(TrayIconKind.Problem, new(p, PhoneLinkWarnings.None, null), En).Length, 1, 127));
    }

    [Fact]
    public void ToastTextFollowsTheRequestedLanguage()
    {
        var g = GuidanceTable.For(PhoneLinkProblem.NoDevice, En)!;
        var xml = ToastXml.ForProblem(g, En);
        Assert.Contains("Cannot connect to the phone", xml);
        Assert.Contains("Retry", xml);
        Assert.Contains("폰에 연결할 수 없습니다", ToastXml.ForProblem(GuidanceTable.For(PhoneLinkProblem.NoDevice, Ko)!, Ko));
    }

    [Theory]
    [InlineData("ko-KR", null, "ko")]
    [InlineData("ko", "auto", "ko")]
    [InlineData("en-US", null, "en")]
    [InlineData("ja-JP", "auto", "en")]
    [InlineData("de-DE", "garbage", "en")]
    [InlineData("en-US", "ko", "ko")]
    [InlineData("ko-KR", "en", "en")]
    public void LanguageResolution(string windowsCulture, string? setting, string expected)
        => Assert.Equal(expected, Language.Resolve(setting, CultureInfo.GetCultureInfo(windowsCulture)));

    [Fact]
    public void ReleaseNotesShowOnlyTheCurrentLanguage()
    {
        const string bilingual = "First release.\n\n### Added\n- A thing\n\n---\n\n첫 릴리스.\n\n### 추가\n- 기능";
        string en = ReleaseNotes.ForLanguage(bilingual, Language.English);
        string ko = ReleaseNotes.ForLanguage(bilingual, Language.Korean);
        Assert.Equal("First release.\n\n### Added\n- A thing", en);
        Assert.Equal("첫 릴리스.\n\n### 추가\n- 기능", ko);
        Assert.DoesNotContain("---", en + ko);
    }

    [Theory]
    [InlineData("- 첫 릴리스 테스트 노트")]                              // 한글만 (구분 없음) → 전부
    [InlineData("Only English notes.\n- a")]                            // 영어만 → 전부
    [InlineData("English A\n\n---\n\nEnglish B")]                       // 구분은 있지만 둘 다 영어 → 전부
    [InlineData("한글 먼저\n\n---\n\nthen English")]                    // 순서가 반대 → 전부
    [InlineData("영문 한글\n\n---\n\n한글 둘")]                          // 앞쪽에 한글이 있으면 병기가 아님 → 전부
    public void NotesWithoutAClearSplitAreShownWhole(string notes)
    {
        Assert.Equal(notes, ReleaseNotes.ForLanguage(notes, Language.English));
        Assert.Equal(notes, ReleaseNotes.ForLanguage(notes, Language.Korean));
    }

    [Fact]
    public void ForLanguageHandlesWindowsLineEndingsAndEmptyNotes()
    {
        Assert.Equal("Hello", ReleaseNotes.ForLanguage("Hello\r\n---\r\n안녕", Language.English));
        Assert.Equal("안녕", ReleaseNotes.ForLanguage("Hello\r\n---\r\n안녕", Language.Korean));
        Assert.Equal("", ReleaseNotes.ForLanguage(null, Language.English));
        Assert.Equal("  ", ReleaseNotes.ForLanguage("  ", Language.Korean));
    }

    [Fact]
    public void EmptyNotesMessageIsLocalized()
    {
        Assert.Contains("No release notes", ReleaseNotes.Clean(null, En));
        Assert.Contains("기록되지", ReleaseNotes.Clean(" ", Ko));
    }

    [Fact]
    public void LanguageSettingIsSavedAndKeepsSkippedVersion()
    {
        string dir = Path.Combine(Path.GetTempPath(), "twinput-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "settings.json");
        try
        {
            var store = new SettingsStore(path);
            Assert.Equal(Language.Auto, store.Language);
            store.SkippedVersion = "0.1.1";
            store.Language = "ko";
            var again = new SettingsStore(path);
            Assert.Equal("ko", again.Language);
            Assert.Equal("0.1.1", again.SkippedVersion);
            again.Language = "nonsense";
            Assert.Equal(Language.Auto, new SettingsStore(path).Language);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
