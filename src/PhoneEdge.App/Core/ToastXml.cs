using System.Security;
using System.Text;

namespace PhoneEdge.App.Core;

/// <summary>Windows 알림(토스트) XML. 클릭·버튼은 arguments 문자열로 앱에 돌아온다.</summary>
public static class ToastXml
{
    public const string ShowGuide = "action=guide";
    public const string Retry = "action=retry";

    public static string ForProblem(Guidance g)
    {
        var sb = new StringBuilder();
        sb.Append($"<toast launch=\"{ShowGuide}\" duration=\"long\">");
        sb.Append("<visual><binding template=\"ToastGeneric\">");
        sb.Append("<text>폰에 연결할 수 없습니다</text>");
        sb.Append($"<text>{SecurityElement.Escape(g.Cause)}</text>");
        sb.Append("</binding></visual>");
        sb.Append("<actions>");
        sb.Append($"<action content=\"해결 방법 보기\" arguments=\"{ShowGuide}\" activationType=\"foreground\"/>");
        if (g.CanRetry) sb.Append($"<action content=\"다시 시도\" arguments=\"{Retry}\" activationType=\"foreground\"/>");
        sb.Append("</actions>");
        sb.Append("</toast>");
        return sb.ToString();
    }
}
