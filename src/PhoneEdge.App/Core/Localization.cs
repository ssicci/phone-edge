using System.Globalization;
using System.Resources;

namespace PhoneEdge.App.Core;

/// <summary>언어 설정 값("auto", "ko", "en")을 실제 언어로 바꾼다. 순수 함수.</summary>
public static class Language
{
    public const string Auto = "auto", Korean = "ko", English = "en";

    /// <summary>알 수 없는 값은 "auto".</summary>
    public static string Normalize(string? setting) => setting switch
    {
        Korean => Korean,
        English => English,
        _ => Auto,
    };

    /// <summary>"ko" 또는 "en". auto 이면 Windows 표시 언어가 ko-* 일 때만 한국어.</summary>
    public static string Resolve(string? setting, CultureInfo uiCulture) => Normalize(setting) switch
    {
        Korean => Korean,
        English => English,
        _ => uiCulture.TwoLetterISOLanguageName == "ko" ? Korean : English,
    };
}

/// <summary>
/// 사용자에게 보이는 문구. Resources\Strings.resx(영어, 기본)와 Strings.ko.resx 에서 읽는다.
/// 새 문구는 두 파일에 함께 추가한다 (테스트가 키 집합과 빈 값을 확인한다).
/// </summary>
public static class L
{
    private static readonly ResourceManager Manager = new("PhoneEdge.App.Resources.Strings", typeof(L).Assembly);
    private static CultureInfo _culture = CultureInfo.GetCultureInfo("en");

    public static string Lang => _culture.TwoLetterISOLanguageName;
    public static CultureInfo Culture => _culture;

    /// <summary>언어가 바뀌면 발생한다 (UI 스레드에서 SetLanguage 를 호출한다).</summary>
    public static event Action? Changed;

    public static CultureInfo CultureFor(string lang) => CultureInfo.GetCultureInfo(lang == Language.Korean ? "ko" : "en");

    /// <summary>설정 값("auto"/"ko"/"en")을 적용한다.</summary>
    public static void SetLanguage(string? setting)
    {
        var next = CultureFor(Language.Resolve(setting, CultureInfo.CurrentUICulture));
        if (next.Name == _culture.Name) return;
        _culture = next;
        Changed?.Invoke();
    }

    /// <summary>키에 해당하는 문구. 없으면 "!키!" (개발 중에 눈에 띄게).</summary>
    public static string T(string key, CultureInfo? culture = null) => Manager.GetString(key, culture ?? _culture) ?? $"!{key}!";

    /// <summary>{0} 같은 자리표시자를 채운다.</summary>
    public static string F(string key, params object[] args) => string.Format(_culture, T(key), args);

    public static string FIn(CultureInfo culture, string key, params object[] args) => string.Format(culture, T(key, culture), args);
}
