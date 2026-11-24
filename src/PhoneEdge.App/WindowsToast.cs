using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PhoneEdge.App.Core;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace PhoneEdge.App;

/// <summary>
/// Windows 알림 센터에 남는 토스트 알림. 설치 패키지가 없는 앱이라 AppUserModelID 를 HKCU 에 직접 등록한다.
/// 알림 클릭과 버튼은 앱이 실행 중일 때 <see cref="Activated"/> 로 돌아온다 (임의의 스레드).
/// </summary>
internal sealed class WindowsToast
{
    private const string Aumid = "PhoneEdge.App";
    private const string Tag = "problem", Group = "phoneedge";
    private readonly ToastNotifier _notifier;

    public event Action<string>? Activated;

    private WindowsToast(ToastNotifier notifier) => _notifier = notifier;

    /// <summary>등록에 실패하면 null (호출자는 다른 방법으로 알린다).</summary>
    public static WindowsToast? TryCreate()
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(Aumid);
            var iconPath = Path.Combine(AppPaths.DataDir, "toast-icon.png");
            var errors = new List<string>();
            bool saved = Core.RetryingFile.Try(() =>
            {
                Directory.CreateDirectory(AppPaths.DataDir);
                using var bmp = TrayIcons.Render(TrayIconKind.Ready, 64);
                bmp.Save(iconPath, ImageFormat.Png);
            }, 5, TimeSpan.FromMilliseconds(300), errors, what: iconPath);
            foreach (var e in errors) Logger.Info("알림 아이콘 저장 실패: " + e);
            // 이전 실행이 만든 아이콘이 있으면 그것을 쓴다. 아이콘이 없어도 알림 자체는 동작한다.
            if (!saved && !File.Exists(iconPath)) iconPath = "";
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\" + Aumid))
            {
                key.SetValue("DisplayName", "PhoneEdge");
                key.SetValue("IconUri", iconPath);
            }
            return new WindowsToast(ToastNotificationManager.CreateToastNotifier(Aumid));
        }
        catch (Exception ex)
        {
            Logger.Info("Windows 알림 등록 실패: " + ex.Message);
            return null;
        }
    }

    /// <summary>문제 알림을 띄운다. 이전 문제 알림은 같은 태그라 교체된다. 실패하면 false.</summary>
    public bool ShowProblem(Guidance g)
    {
        try
        {
            // 사용자가 Windows 설정에서 알림을 꺼 두면 Show 는 예외 없이 아무것도 띄우지 않는다.
            if (_notifier.Setting != NotificationSetting.Enabled)
            {
                Logger.Info("Windows 알림이 꺼져 있음: " + _notifier.Setting);
                return false;
            }
            var doc = new XmlDocument();
            doc.LoadXml(ToastXml.ForProblem(g));
            var toast = new ToastNotification(doc) { Tag = Tag, Group = Group };
            toast.Activated += (_, e) => Activated?.Invoke((e as ToastActivatedEventArgs)?.Arguments ?? ToastXml.ShowGuide);
            _notifier.Show(toast);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Info("Windows 알림 표시 실패: " + ex.Message);
            return false;
        }
    }

    /// <summary>문제가 풀리면 알림 센터에 남은 문제 알림을 지운다.</summary>
    public void ClearProblem()
    {
        try { ToastNotificationManager.History.Remove(Tag, Group, Aumid); } catch { }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(string appId);
}
