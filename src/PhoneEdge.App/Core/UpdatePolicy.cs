using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PhoneEdge.App.Core;

/// <summary>X.Y.Z[-접미사] 버전. 접미사가 있으면 같은 숫자의 정식 버전보다 낮다.</summary>
public readonly record struct AppVersion(int Major, int Minor, int Patch, string Pre) : IComparable<AppVersion>
{
    public static bool TryParse(string? text, out AppVersion version)
    {
        version = default;
        if (text is null) return false;
        var m = Regex.Match(text.Trim(), @"^v?(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.\-]+))?(?:\+.*)?$");
        if (!m.Success) return false;
        version = new AppVersion(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), m.Groups[4].Value);
        return true;
    }

    public int CompareTo(AppVersion o)
    {
        int c = Major.CompareTo(o.Major);
        if (c == 0) c = Minor.CompareTo(o.Minor);
        if (c == 0) c = Patch.CompareTo(o.Patch);
        if (c != 0) return c;
        if (Pre == o.Pre) return 0;
        if (Pre.Length == 0) return 1;      // 정식 > 접미사 있는 버전
        if (o.Pre.Length == 0) return -1;
        return string.CompareOrdinal(Pre, o.Pre);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}" + (Pre.Length > 0 ? "-" + Pre : "");
}

public static class UpdatePolicy
{
    /// <summary>
    /// 새 버전을 사용자에게 보여 줄지. candidate 가 현재보다 높아야 하고,
    /// 자동 확인은 사용자가 건너뛴 버전을 다시 보여 주지 않는다 (수동 확인은 보여 준다).
    /// </summary>
    public static bool ShouldOffer(string? current, string? candidate, string? skipped, bool manual)
    {
        if (!AppVersion.TryParse(candidate, out var cand)) return false;
        if (AppVersion.TryParse(current, out var cur) && cand.CompareTo(cur) <= 0) return false;
        if (manual) return true;
        return !(AppVersion.TryParse(skipped, out var skip) && skip.CompareTo(cand) == 0);
    }
}

/// <summary>GitHub 릴리스 노트(마크다운)를 업데이트 창의 일반 텍스트로 다듬는다.</summary>
public static class ReleaseNotes
{
    public const int MaxLength = 6000;

    /// <summary>
    /// 영문 → 가로줄(---) → 한글 순서로 병기된 노트에서 지정한 언어 쪽만 꺼낸다.
    /// 앞쪽에 한글이 없고 뒤쪽에 한글이 있을 때만 병기로 보고, 그렇지 않은 노트(구분이 없거나 한 언어뿐)는 그대로 돌려준다.
    /// </summary>
    public static string ForLanguage(string? markdown, string lang)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return markdown ?? "";
        string s = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var m = Regex.Match(s, @"^[ \t]*---+[ \t]*$", RegexOptions.Multiline);
        if (!m.Success) return markdown;
        string first = s[..m.Index], second = s[(m.Index + m.Length)..];
        if (HasHangul(first) || !HasHangul(second)) return markdown;
        return (lang == Language.Korean ? second : first).Trim();
    }

    private static bool HasHangul(string text) => text.Any(c => c is >= '\uAC00' and <= '\uD7A3');

    public static string Clean(string? markdown, System.Globalization.CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return L.T("Update_NoNotes", culture);
        string s = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        s = Regex.Replace(s, @"<!--.*?-->", "", RegexOptions.Singleline);
        var lines = new List<string>();
        foreach (var raw in s.Split('\n'))
        {
            string line = raw.TrimEnd();
            if (Regex.IsMatch(line, @"^\s*(-{3,}|\*{3,}|_{3,})\s*$")) { lines.Add("────────────────────"); continue; }   // 가로줄
            line = Regex.Replace(line, @"^\s{0,3}#{1,6}\s*", "");                 // 제목 표시
            line = Regex.Replace(line, @"^(\s*)[-*+]\s+", "$1• ");                // 목록 표시
            line = Regex.Replace(line, @"\*\*(.+?)\*\*|__(.+?)__", "$1$2");        // 굵게
            line = Regex.Replace(line, @"`([^`]*)`", "$1");                       // 코드
            line = Regex.Replace(line, @"\[([^\]]+)\]\([^)]+\)", "$1");           // 링크
            lines.Add(line);
        }
        s = Regex.Replace(string.Join("\n", lines), @"\n{3,}", "\n\n").Trim();
        if (s.Length > MaxLength) s = s[..MaxLength].TrimEnd() + "\n…";
        return s.Replace("\n", Environment.NewLine);
    }
}

/// <summary>%LocalAppData%\PhoneEdge\settings.json. 지금은 건너뛴 버전 하나만 저장한다.</summary>
public sealed class SettingsStore
{
    private sealed class Data
    {
        public string? SkippedVersion { get; set; }
        /// <summary>"auto"(기본), "ko", "en".</summary>
        public string? Language { get; set; }
    }

    private readonly string _path;
    public SettingsStore(string path) => _path = path;

    public string? SkippedVersion
    {
        get => Read().SkippedVersion;
        set { var d = Read(); d.SkippedVersion = value; Write(d); }
    }

    public string Language
    {
        get => Core.Language.Normalize(Read().Language);
        set { var d = Read(); d.Language = Core.Language.Normalize(value); Write(d); }
    }

    private Data Read()
    {
        try
        {
            return File.Exists(_path) ? JsonSerializer.Deserialize<Data>(File.ReadAllText(_path, Encoding.UTF8)) ?? new() : new();
        }
        catch { return new(); }   // 깨진 설정 파일은 없는 것으로 본다
    }

    private void Write(Data d)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }
        catch { /* 저장 실패는 치명적이지 않다 */ }
    }
}
