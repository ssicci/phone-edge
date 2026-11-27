# Changelog / 변경 내용

Each version has a `## [X.Y.Z] - date` entry: the English notes first, then a `---` line and the Korean notes. `## [Unreleased]` collects the changes for the next release; `scripts/release.ps1 X.Y.Z` renames it to the new version and date. The app's update window (current language only) and the GitHub release notes show the entry as written (the app UI is available in Korean and English).
버전마다 `## [X.Y.Z] - 날짜` 항목을 두고, 영문 설명 다음에 `---` 줄과 한글 설명을 씁니다. `## [Unreleased]`에는 다음 릴리스의 변경 내용을 모아 두고, `scripts/release.ps1 X.Y.Z`가 이를 새 버전과 날짜로 바꿉니다. 업데이트 창에는 현재 언어 부분만, GitHub 릴리스 노트에는 이 내용이 그대로 나옵니다.

## [0.8.0] - 2026-11-27

### Changed
- **PhoneEdge is now Twinput** (twin + input). The app, the installer, the tray, windows, notifications and the phone helper app's visible name ("Twinput Companion") all use the new name. The helper app keeps its Android application id, so an installed helper app keeps updating and keeps its permission.
- The installer id changed, so **an installed PhoneEdge does not update itself to Twinput**: install Twinput once from the new setup file (`Twinput-win-x64-Setup.exe`). The first time Twinput starts it copies your settings (language, phone positions, helper-app state, options) from PhoneEdge's folder, turns on **Start with Windows** if PhoneEdge had it on and removes PhoneEdge's entry, and shows a one-time notice (asking you to exit PhoneEdge first if it is still running). PhoneEdge itself is not touched: remove it in **Settings → Apps → Installed apps**.
- Updates now use their own channel (`win-x64`) so an old PhoneEdge can never download a Twinput package and apply it over itself.
- The GitHub repository is now `ssicci/twinput`, the log file is `twinput.log`, and the developer update-source variable is `TWINPUT_UPDATE_SOURCE`.

---

### 변경
- **PhoneEdge가 Twinput(트윈풋, twin + input)으로 이름이 바뀌었습니다.** 앱, 설치 프로그램, 트레이, 창, 알림, 폰 도우미 앱의 보이는 이름("Twinput 도우미")이 모두 새 이름을 씁니다. 도우미 앱은 안드로이드 앱 ID를 그대로 두므로, 설치된 도우미 앱은 계속 업데이트되고 권한도 유지됩니다.
- 설치 프로그램 ID가 바뀌어서 **설치된 PhoneEdge는 저절로 Twinput으로 업데이트되지 않습니다.** 새 설치 파일(`Twinput-win-x64-Setup.exe`)로 Twinput을 한 번 설치하세요. Twinput은 처음 실행할 때 PhoneEdge 폴더에서 설정(언어, 폰 위치, 도우미 앱 상태, 옵션)을 복사해 오고, PhoneEdge의 **Windows 시작 시 자동 실행**이 켜져 있었다면 Twinput의 것을 켜고 PhoneEdge의 등록을 지우며, 안내를 한 번 보여 줍니다(PhoneEdge가 아직 실행 중이면 먼저 종료하라고 알려 줍니다). PhoneEdge 자체는 건드리지 않으니 **설정 → 앱 → 설치된 앱**에서 제거하세요.
- 업데이트는 이제 자체 채널(`win-x64`)을 써서, 옛 PhoneEdge가 Twinput 패키지를 받아 자기 위에 적용하는 일이 없도록 했습니다.
- GitHub 저장소는 `ssicci/twinput`, 로그 파일은 `twinput.log`, 개발자용 업데이트 소스 환경 변수는 `TWINPUT_UPDATE_SOURCE`입니다.

## [0.7.1] - 2026-11-26

### Fixed
- In the phone position window, **Save** and **Cancel** did nothing, so the new position was never saved.

---

### 수정
- 폰 위치 설정 창에서 **저장**과 **취소** 버튼을 눌러도 아무 반응이 없어 새 위치가 저장되지 않던 문제를 고쳤습니다.

## [0.7.0] - 2026-11-26

### Added
- **Phone position window** (tray menu → **Phone position…**): the phone can now be on any of the four sides of your monitors, not only to the right or left. The window draws your monitors as numbered boxes; drag the phone onto a free edge and it snaps to the whole free part of that edge. An edge that touches another monitor cannot be used, and with monitors of different size only the untouched part is offered. **Identify** shows a big number on each monitor for 2 seconds.
- The phone position is remembered **per set of monitors** (for example office and home). Monitors are recognized by the identity they report (model and serial number), so a changed resolution or port does not matter. A set PhoneEdge has not seen before starts with the default (right edge of the rightmost monitor) and shows a notification asking you to set the position; if notifications are off, the window opens by itself. The 0.6 left/right choice is carried over to your current monitors.
- **Accidental entry guard:** the ends of the phone position (10% each) no longer enter phone mode, so window close buttons, the Start button and taskbar corners keep working, and you must push about 120 mouse counts past the edge (moving back, or pausing for half a second, starts over). The values can be changed with `EntryGuard` in `settings.json`.

### Changed
- When the monitors change (plugging, unplugging, rearranging), PhoneEdge returns to the laptop first and picks the position for the new set of monitors. The tray menu's **Phone position** submenu (right/left) is replaced by the window above.

---

### 추가
- **폰 위치 설정 창**(트레이 메뉴 → **폰 위치 설정…**): 이제 폰을 오른쪽·왼쪽뿐 아니라 모니터의 네 방향 어디에나 둘 수 있습니다. 창은 모니터를 번호가 붙은 상자로 그려 주고, 폰을 빈 가장자리로 끌면 그 가장자리의 빈 구간 전체에 붙습니다. 다른 모니터와 맞닿은 가장자리는 쓸 수 없고, 모니터 크기가 다르면 닿지 않은 부분만 고를 수 있습니다. **식별**을 누르면 각 모니터에 큰 번호를 2초간 띄웁니다.
- 폰 위치는 **모니터 구성마다**(예: 회사와 집) 기억합니다. 모니터가 알리는 정보(모델과 시리얼 번호)로 모니터를 알아보므로 해상도나 연결 포트가 바뀌어도 상관없습니다. PhoneEdge가 처음 보는 구성은 기본값(가장 오른쪽 모니터의 오른쪽 끝)으로 시작하고, 폰 위치를 정해 달라는 알림을 띄웁니다(알림이 꺼져 있으면 설정 창이 바로 열립니다). 0.6의 왼쪽/오른쪽 선택은 현재 모니터 구성으로 이전됩니다.
- **오진입 방지:** 폰 자리 양 끝(각 10%)에서는 폰 모드로 들어가지 않아 창 닫기 단추, 시작 단추, 작업 표시줄 모서리가 그대로 동작하고, 가장자리를 넘어 마우스 카운트를 약 120만큼 더 밀어야 들어갑니다(반대로 움직이거나 0.5초 멈추면 처음부터). `settings.json`의 `EntryGuard`로 값을 바꿀 수 있습니다.

### 변경
- 모니터가 바뀌면(꽂기·뽑기·배치 변경) PhoneEdge가 먼저 노트북으로 돌아오고, 새 모니터 구성의 폰 위치를 고릅니다. 트레이 메뉴의 **폰 위치** 하위 메뉴(오른쪽/왼쪽)는 위 설정 창으로 바뀌었습니다.

## [0.6.1] - 2026-11-26

### Fixed
- Checking for updates failed with "connection refused" when Windows has a proxy set manually (for example a company proxy) and the laptop is on another network such as a phone hotspot. PhoneEdge still uses the Windows proxy first, and if it cannot reach the proxy it now retries the same request directly.

---

### 수정
- Windows에 프록시가 수동으로 설정된 노트북(예: 회사 프록시)을 휴대폰 핫스팟 같은 다른 네트워크에 연결하면, 업데이트 확인이 "연결을 거부했으므로 연결하지 못했습니다"로 실패하던 문제를 고쳤습니다. 지금처럼 Windows 프록시를 먼저 쓰고, 프록시에 닿지 못하면 같은 요청을 직접 연결로 다시 보냅니다.

## [0.6.0] - 2026-11-26

### Added
- **Phone position** setting in the tray menu: put the phone to the **left** of the laptop as well as to the right (the default). With the phone on the left, push the cursor past the left edge of the leftmost monitor to enter; it appears at the phone's right edge, and pushing past the phone's right edge returns you to the same height at the laptop's left edge. The choice is saved and applies immediately (changing it in phone mode returns to the laptop first). Above/below is not supported yet.

---

### 추가
- 트레이 메뉴의 **폰 위치** 설정: 폰을 노트북 오른쪽(기본)뿐 아니라 **왼쪽**에도 둘 수 있습니다. 폰이 왼쪽에 있으면 가장 왼쪽 모니터의 왼쪽 끝 밖으로 밀어 들어가고, 커서는 폰의 오른쪽 끝에서 나타나며, 폰의 오른쪽 끝 밖으로 밀면 노트북 왼쪽 끝의 같은 높이로 돌아옵니다. 선택은 저장되고 바로 적용됩니다(폰 모드 중에 바꾸면 먼저 노트북으로 돌아옵니다). 위·아래 배치는 아직 지원하지 않습니다.

## [0.5.3] - 2026-11-25

### Fixed
- The mouse could not cross over to the phone when a tool such as PowerToys Mouse Without Borders was running. Those tools make Windows report real mouse movement without a device handle, and PhoneEdge discarded such movement as synthetic input, so it never saw the cursor reach the screen edge.

---

### 수정
- PowerToys Mouse Without Borders 같은 도구가 실행 중이면 마우스가 폰으로 넘어가지 않던 문제를 고쳤습니다. 이런 도구가 있으면 Windows가 실제 마우스 움직임을 장치 정보 없이 전달하는데, PhoneEdge가 이를 프로그램이 만든 입력으로 보고 버려서 커서가 화면 끝에 닿은 것을 알지 못했습니다.

## [0.5.2] - 2026-11-25

### Changed
- An installed phone helper app is now updated automatically when PhoneEdge brings a newer one, the next time the phone is connected, without asking. It waits while you are using the phone, tries once per connection, and leaves **Update phone helper app…** in the menu if it fails. The first install still asks for confirmation.

---

### 변경
- 폰 도우미 앱이 이미 설치되어 있으면, PhoneEdge에 더 새로운 버전이 들어올 때 다음 연결에서 묻지 않고 자동으로 업데이트합니다. 폰을 쓰는 중이면 끝날 때까지 기다리고, 연결 한 번에 한 번만 시도하며, 실패하면 메뉴에 **폰 도우미 앱 업데이트…**를 남겨 둡니다. 처음 설치할 때는 지금처럼 확인을 받습니다.

## [0.5.1] - 2026-11-25

### Changed
- When the phone connects while the help window is open, the window now says the phone is connected and closes by itself after 3 seconds. It stays open if a setting still needs your attention (pointer acceleration).

### Fixed
- With the phone companion app installed, plugging in the cable no longer pops up a "USB debugging is off" help window. The companion turns USB debugging on right away, but the laptop takes 2–3 seconds to see the phone; PhoneEdge now waits up to 10 seconds before calling it a problem.

---

### 변경
- 안내 창이 열려 있을 때 폰이 연결되면 "연결되었습니다"로 바뀌고 3초 뒤 저절로 닫힙니다. 확인할 설정(포인터 가속)이 남아 있으면 닫지 않고 열어 둡니다.

### 수정
- 폰 도우미 앱이 설치되어 있으면 케이블을 꽂을 때마다 "USB 디버깅이 꺼져 있습니다" 안내 창이 뜨던 문제를 고쳤습니다. 도우미 앱이 바로 USB 디버깅을 켜지만 노트북이 폰을 알아보기까지 2~3초가 걸리기 때문으로, 이제 최대 10초까지 기다린 뒤에 문제로 판단합니다.

## [0.5.0] - 2026-11-25

### Added
- Optional phone helper app ("PhoneEdge Companion"): when the phone is ready, the tray menu offers **Install phone helper app…** (and **Update phone helper app…** when a newer one is bundled). The helper app turns the phone's USB debugging on when a USB data connection appears and off when the cable is unplugged, so banking and securities apps work as soon as you unplug. PhoneEdge installs it over USB, gives it its one permission and excludes it from battery optimization. The signed helper app is built in the release workflow and shipped with the installer.

---

### 추가
- 선택 사항인 폰 도우미 앱("PhoneEdge Companion"): 폰이 준비되면 트레이 메뉴에 **폰 도우미 앱 설치…**(더 새로운 앱이 들어 있으면 **폰 도우미 앱 업데이트…**)가 나타납니다. 도우미 앱은 USB 데이터 연결이 생기면 폰의 USB 디버깅을 켜고 케이블을 뽑으면 꺼서, 뽑는 즉시 은행·증권 앱이 동작합니다. PhoneEdge가 USB로 설치하고, 필요한 권한 하나를 주고, 배터리 최적화에서 제외합니다. 서명된 도우미 앱은 릴리스 워크플로에서 빌드해 설치 프로그램에 함께 들어갑니다.

## [0.4.0] - 2026-11-25

### Added
- When you exit PhoneEdge (tray menu "Exit", or signing out / shutting down Windows) while the phone is connected, it now turns off the phone's USB debugging, because banking and securities apps often refuse to run while it is on. It is on by default and can be switched off with "Turn off phone USB debugging on exit" in the tray menu. It does not do this for update restarts or crashes. Unplugging the cable alone does not turn USB debugging off, and to use PhoneEdge again you must turn it back on in the phone's Developer options.

---

### 추가
- 폰이 연결된 상태에서 PhoneEdge를 종료하면(트레이 메뉴 "종료", Windows 로그오프·종료) 폰의 USB 디버깅을 끕니다. 은행·증권 앱이 USB 디버깅이 켜져 있으면 실행을 거부하는 경우가 많기 때문입니다. 기본값은 켜짐이고, 트레이 메뉴의 "종료할 때 폰 USB 디버깅 끄기"로 끌 수 있습니다. 업데이트 재시작이나 오류 종료에서는 끄지 않습니다. 케이블을 뽑는 것만으로는 USB 디버깅이 꺼지지 않으며, PhoneEdge를 다시 쓰려면 폰의 개발자 옵션에서 직접 다시 켜야 합니다.

## [0.3.0] - 2026-11-25

### Added
- "Start with Windows" in the tray menu (off by default, installed app only). It follows the choice you make in Windows Settings → Apps → Startup, and uninstalling removes it.
- "Open install folder" in the tray menu.

---

### 추가
- 트레이 메뉴에 "Windows 시작 시 자동 실행"을 추가했습니다 (기본값 꺼짐, 설치본에서만). Windows 설정 → 앱 → 시작 프로그램에서 바꾼 내용도 따르며, 제거하면 등록도 지워집니다.
- 트레이 메뉴에 "설치 폴더 열기"를 추가했습니다.

## [0.2.1] - 2026-11-24

### Fixed
- Right after an install or an update, the app could not write its log file or the Windows notification icon, so the log was empty and Windows notifications were off for that run. The app now retries for a few seconds, falls back to the TEMP folder for the log if it still cannot write, and records why it failed at the top of the log.

---

### 수정
- 설치나 업데이트 직후 다시 시작된 앱이 로그 파일과 Windows 알림 아이콘을 쓰지 못해, 그 실행 동안 로그가 비고 Windows 알림이 꺼지던 문제를 고쳤습니다. 이제 몇 초 동안 다시 시도하고, 그래도 안 되면 로그를 TEMP 폴더에 쓰며, 실패 이유를 로그 맨 앞에 남깁니다.

## [0.2.0] - 2026-11-23

### Added
- The app can now be shown in English or Korean. It follows the Windows display language by default (Korean for Korean Windows, English otherwise), and the tray menu has a "언어 / Language" submenu (Auto / 한국어 / English) to choose. The choice is saved and applies immediately to the tray menu, tooltip and open windows.
- The update window shows the release notes in the current language only.

---

### 추가
- 앱 화면을 영어 또는 한국어로 표시할 수 있습니다. 기본값은 Windows 표시 언어를 따르고(한국어 Windows는 한국어, 그 외는 영어), 트레이 메뉴의 "언어 / Language" 하위 메뉴(자동 / 한국어 / English)에서 고를 수 있습니다. 선택은 저장되며 트레이 메뉴, 툴팁, 열려 있는 창에 바로 적용됩니다.
- 업데이트 창은 변경 내용을 현재 언어 부분만 보여 줍니다.

## [0.1.0] - 2026-11-23

First release.

### Added
- Push the laptop cursor past the right edge of the screen and the mouse and keyboard move to a USB-connected Android phone (Galaxy Z Fold7 is the reference device). Push the phone cursor past the phone's left edge and they come back.
- Keyboard: Hangul/Hanja keys, right Alt/Ctrl, arrow keys and other extended keys. Mouse: movement, clicks, wheel and back/forward buttons.
- `Ctrl+Alt+Esc` returns to the laptop at any time, and unplugging the phone returns automatically.
- Nothing is installed on the phone. Only USB debugging has to be turned on.
- A tray icon shows the state (not connected, ready, phone mode, paused, problem), with Pause and Exit in its menu.
- When the phone cannot connect, a Windows notification and a help window explain the cause and the fix, with a Retry button and an automatic fix for the phone's pointer acceleration setting.
- Installer (no administrator rights needed) and automatic updates: when a new version is released the app shows what changed and, if you accept, downloads it and restarts.

---

첫 릴리스.

### 추가
- 노트북 커서를 화면 오른쪽 끝 밖으로 밀면 USB로 연결된 안드로이드 폰(갤럭시 Z 폴드7 기준)으로 마우스·키보드 입력이 넘어가고, 폰 커서가 폰 화면 왼쪽 끝을 넘으면 노트북으로 돌아옵니다.
- 키보드는 한/영, 한자, 오른쪽 Alt/Ctrl, 방향키 등을 지원하고, 마우스는 이동·클릭·휠·뒤로/앞으로 버튼을 지원합니다.
- `Ctrl+Alt+Esc`로 언제든 노트북으로 즉시 돌아옵니다. 폰이 끊겨도 자동으로 돌아옵니다.
- 폰에는 앱을 설치하지 않습니다. USB 디버깅만 켜면 됩니다.
- 트레이 아이콘으로 상태(연결 안 됨, 준비됨, 폰 모드, 일시 정지, 문제 있음)를 보여 주고, 일시 정지·종료 메뉴를 제공합니다.
- 폰에 연결하지 못하면 원인과 해결 방법을 Windows 알림과 안내 창으로 알려 주고, 다시 시도와 폰 설정 자동 조치 버튼을 제공합니다.
- 설치 프로그램(관리자 권한 불필요)과 자동 업데이트: 새 버전이 나오면 변경 내용을 보여 주고, 수락하면 내려받아 다시 시작합니다.
