# PhoneEdge

노트북 커서를 화면 오른쪽 끝 밖으로 밀면 USB로 연결된 안드로이드 폰으로 마우스·키보드 입력이 넘어가고, 폰 커서가 폰 화면 왼쪽 끝을 넘으면 노트북으로 돌아오는 Windows 트레이 앱입니다. 폰에는 아무것도 설치하지 않습니다. (기준 기기: 갤럭시 Z 폴드7)

## 설치

1. [최신 릴리스](https://github.com/ssicci/phone-edge/releases/latest)에서 `PhoneEdge-win-Setup.exe`를 받아 실행합니다. 관리자 권한은 필요 없고, .NET을 따로 설치하지 않아도 됩니다.
2. 설치가 끝나면 시작 메뉴의 PhoneEdge로 실행합니다. 트레이(작업 표시줄 오른쪽 아래)에 폰 모양 아이콘이 나타납니다.

> **SmartScreen 경고**: 설치 파일에 코드 서명이 없어서 "Windows의 PC 보호" 창이 뜰 수 있습니다. "추가 정보 → 실행"을 누르면 설치됩니다.

새 버전이 나오면 앱이 알아채서 변경 내용을 보여 줍니다. [지금 업데이트]를 누르면 내려받아 자동으로 다시 시작합니다. (시작 10초 뒤와 6시간마다 확인하며, 트레이 메뉴의 "업데이트 확인"으로 직접 확인할 수도 있습니다.)

## 폰 처음 설정

1. 설정 → 휴대전화 정보 → 소프트웨어 정보 → **빌드번호**를 7번 눌러 개발자 옵션을 켭니다.
2. 설정 → 개발자 옵션 → **USB 디버깅**을 켭니다. (켤 수 없으면 설정 → 보안 및 개인정보 보호 → 보안 위험 자동 차단을 끕니다.)
3. 폰을 데이터 전송이 되는 USB 케이블로 연결하고, 폰에 뜨는 "USB 디버깅을 허용하시겠습니까?"에서 **이 컴퓨터에서 항상 허용**을 체크하고 허용합니다.
4. (권장) 폰의 **포인터 가속**을 끕니다. 켜져 있으면 마우스를 빠르게 움직일 때 노트북으로 돌아오는 시점이 늦어집니다. 앱이 "폰 설정 자동으로 끄기" 버튼을 제공하고, 직접 하려면 설정 → 일반 → 마우스 및 트랙패드(기기에 따라 이름이 다를 수 있음)에서 끕니다.

연결이 안 되면 트레이 아이콘이 빨갛게 바뀌고, 원인과 해결 방법을 알림과 "연결 문제 해결…" 창으로 안내합니다.

## 사용법

- 폰이 준비되면(초록 체크) 노트북 커서를 가장 오른쪽 모니터의 오른쪽 끝으로 밀면 폰 모드(파란 화살표)가 됩니다.
- 폰 화면 왼쪽 끝 밖으로 커서를 밀면 노트북으로 돌아옵니다.
- **`Ctrl+Alt+Esc`**: 언제든 즉시 노트북으로 돌아옵니다. 폰 케이블이 빠져도 자동으로 돌아옵니다.
- 트레이 메뉴: 일시 정지, 연결 문제 해결, 업데이트 확인, 종료.

## 개발

```powershell
pwsh tools/fetch-tools.ps1                                   # adb, scrcpy 서버 받기 (해시 검증)
dotnet build src/PhoneEdge.App/PhoneEdge.App.csproj
dotnet test tests/PhoneEdge.App.Tests/PhoneEdge.App.Tests.csproj
src/PhoneEdge.App/bin/Debug/net8.0-windows10.0.19041.0/PhoneEdge.exe --fake   # 폰 없이 가짜 폰으로 실행
```

- 용어는 [CONTEXT.md](CONTEXT.md), 결정 사항과 폴더 소유권은 [CLAUDE.md](CLAUDE.md)를 봅니다.
- 개발 실행(`bin`에서 실행)은 업데이트를 확인하지 않고 메뉴에 "(개발 실행)"으로 표시됩니다.
- 업데이트를 로컬에서 시험하려면 설치본을 실행하기 전에 환경 변수 `PHONEEDGE_UPDATE_SOURCE`에 `vpk pack`이 만든 폴더(`Releases`)를 지정합니다 (개발자 전용).

## 릴리스 절차

1. `CHANGELOG.md`에 `## [X.Y.Z] - 날짜` 항목을 씁니다. 이 내용이 업데이트 창과 GitHub 릴리스 노트가 됩니다.
2. `pwsh scripts/release.ps1 X.Y.Z` — `Directory.Build.props`의 버전을 올리고 CHANGELOG 항목을 확인한 뒤, 실행할 git 명령을 출력합니다 (스크립트가 git을 실행하지는 않습니다).
3. 출력된 명령으로 커밋하고 `vX.Y.Z` 태그를 push 합니다.
4. 태그가 push 되면 `.github/workflows/release.yml`이 도구 받기 → 테스트 → publish → 이전 릴리스 받기(델타용) → `vpk pack` → GitHub 릴리스 업로드까지 자동으로 합니다. 태그 버전이 `Directory.Build.props`나 CHANGELOG와 다르면 실패합니다.

설치된 앱은 정식 릴리스(프리릴리스 제외)만 업데이트 대상으로 봅니다.

## 제3자 소프트웨어

[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) 참고. scrcpy 서버(Apache-2.0), Android platform-tools(adb), Velopack(MIT)을 사용합니다.
