using System.Security;
using System.Text;

namespace Twinput.App.Core;

/// <summary>Windows 알림(토스트) XML. 클릭·버튼은 arguments 문자열로 앱에 돌아온다.</summary>
public static class ToastXml
{
    public const string ShowGuide = "action=guide";
    public const string Retry = "action=retry";
    public const string Layout = "action=layout";

    /// <summary>처음 보는 모니터 구성에서 폰 위치를 정해 달라는 알림. 클릭하면 설정 창이 열린다.</summary>
    public static string ForLayoutPrompt(System.Globalization.CultureInfo? culture = null)
    {
        var sb = new StringBuilder();
        sb.Append($"<toast launch=\"{Layout}\">");
        sb.Append("<visual><binding template=\"ToastGeneric\">");
        sb.Append($"<text>{SecurityElement.Escape(L.T("Menu_PhoneSlot", culture))}</text>");
        sb.Append($"<text>{SecurityElement.Escape(L.T("Toast_LayoutText", culture))}</text>");
        sb.Append("</binding></visual>");
        sb.Append("<actions>");
        sb.Append($"<action content=\"{SecurityElement.Escape(L.T("Toast_LayoutOpen", culture))}\" arguments=\"{Layout}\" activationType=\"foreground\"/>");
        sb.Append("</actions>");
        sb.Append("</toast>");
        return sb.ToString();
    }

    public static string ForProblem(Guidance g, System.Globalization.CultureInfo? culture = null)
    {
        var sb = new StringBuilder();
        sb.Append($"<toast launch=\"{ShowGuide}\" duration=\"long\">");
        sb.Append("<visual><binding template=\"ToastGeneric\">");
        sb.Append($"<text>{SecurityElement.Escape(L.T("Toast_Title", culture))}</text>");
        sb.Append($"<text>{SecurityElement.Escape(g.Cause)}</text>");
        sb.Append("</binding></visual>");
        sb.Append("<actions>");
        sb.Append($"<action content=\"{SecurityElement.Escape(L.T("Toast_ShowGuide", culture))}\" arguments=\"{ShowGuide}\" activationType=\"foreground\"/>");
        if (g.CanRetry) sb.Append($"<action content=\"{SecurityElement.Escape(L.T("Toast_Retry", culture))}\" arguments=\"{Retry}\" activationType=\"foreground\"/>");
        sb.Append("</actions>");
        sb.Append("</toast>");
        return sb.ToString();
    }
}
