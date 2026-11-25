# Changelog / 변경 내용

Each version has a `## [X.Y.Z] - date` entry: the English notes first, then a `---` line and the Korean notes. `## [Unreleased]` collects the changes for the next release; `scripts/release.ps1 X.Y.Z` renames it to the new version and date. The app's update window (current language only) and the GitHub release notes show the entry as written (the app UI is available in Korean and English).
버전마다 `## [X.Y.Z] - 날짜` 항목을 두고, 영문 설명 다음에 `---` 줄과 한글 설명을 씁니다. `## [Unreleased]`에는 다음 릴리스의 변경 내용을 모아 두고, `scripts/release.ps1 X.Y.Z`가 이를 새 버전과 날짜로 바꿉니다. 업데이트 창에는 현재 언어 부분만, GitHub 릴리스 노트에는 이 내용이 그대로 나옵니다.

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
