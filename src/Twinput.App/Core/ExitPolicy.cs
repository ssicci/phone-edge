using Twinput.Contracts;

namespace Twinput.App.Core;

/// <summary>앱이 끝나는 이유.</summary>
public enum ExitReason
{
    /// <summary>트레이 메뉴의 "종료".</summary>
    UserExit,
    /// <summary>Windows 로그오프 또는 종료.</summary>
    SessionEnding,
    /// <summary>업데이트를 적용하려는 재시작. 곧 다시 시작해 폰에 연결해야 하므로 USB 디버깅을 끄면 안 된다.</summary>
    Update,
    /// <summary>예외, 프로세스 종료 등 그 밖의 경우.</summary>
    Other,
}

/// <summary>종료할 때 폰의 USB 디버깅을 끌지 판단한다. 순수 로직.</summary>
public static class ExitPolicy
{
    /// <summary>
    /// 설정이 켜져 있고, 폰이 준비된(Ready) 상태이고, 사용자가 직접 끝내거나 Windows 가 끝날 때만 끈다.
    /// 업데이트 재시작과 예외 종료에서는 끄지 않는다. 개발 실행(설치본이 아님)에서는 끄지 않는데,
    /// 가짜 폰으로 흐름을 확인하는 경우는 예외다 (실제 폰에 영향이 없다).
    /// 이미 실행 중이라 새 인스턴스가 바로 끝나는 경우는 앱 객체가 만들어지기 전이라 여기까지 오지 않는다.
    /// </summary>
    public static bool ShouldDisableUsbDebugging(ExitReason reason, bool settingEnabled, bool linkReady, bool isInstalled, bool isFakeLink)
    {
        if (!settingEnabled || !linkReady) return false;
        if (!isInstalled && !isFakeLink) return false;
        return reason is ExitReason.UserExit or ExitReason.SessionEnding;
    }

    /// <summary>끄기 명령을 기다리는 최대 시간.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// 정책에 맞으면 폰에 USB 디버깅 끄기를 요청하고 최대 <paramref name="timeout"/> 기다린다.
    /// 실패, 시간 초과, 예외는 로그만 남기고 삼킨다 (종료를 막지 않는다). 호출했으면 true.
    /// </summary>
    public static bool DisableUsbDebuggingIfNeeded(IPhoneLink link, ExitReason reason, bool settingEnabled, bool isInstalled,
        Action<string> log, TimeSpan? timeout = null)
    {
        try
        {
            bool should = ShouldDisableUsbDebugging(reason, settingEnabled, link.State == PhoneLinkState.Ready, isInstalled, link is FakePhoneLink);
            if (!should)
            {
                log($"exit ({reason}): USB debugging left as is");
                return false;
            }
            log($"exit ({reason}): turning off phone USB debugging");
            // UI 스레드를 붙잡은 채 기다리므로, 엔진이 UI 스레드를 필요로 해도 막히지 않게 스레드 풀에서 실행한다.
            var task = Task.Run(() => link.TryFixAsync(PhoneLinkFix.DisableUsbDebugging));
            if (!task.Wait(timeout ?? Timeout)) log("USB debugging off: timed out");
            else log("USB debugging off: " + (task.Result ? "done" : "failed"));
            return true;
        }
        catch (Exception ex)
        {
            log("USB debugging off failed: " + (ex.InnerException ?? ex).Message);
            return true;
        }
    }
}
