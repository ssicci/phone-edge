# Changelog / 변경 내용

Each version has a `## [X.Y.Z] - date` entry: the English notes first, then a `---` line and the Korean notes. The app's update window and the GitHub release notes show the entry as written, because the app UI is in Korean.
버전마다 `## [X.Y.Z] - 날짜` 항목을 두고, 영문 설명 다음에 `---` 줄과 한글 설명을 씁니다. 앱 화면이 한국어이므로 업데이트 창과 GitHub 릴리스 노트에 이 내용이 그대로 나옵니다.

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
