# PhoneEdge (가칭)

노트북 커서가 화면 우측 경계를 넘으면 마우스·키보드 입력이 USB로 연결된 안드로이드 폰으로 넘어가는 Windows 트레이 앱.
용어는 `CONTEXT.md`를 따른다. 상위 폴더에 다른 `CONTEXT.md`가 있어도 이 프로젝트에 적용하지 않는다.

## 확정된 결정

- 기준 기기: 삼성 갤럭시 Z 폴드7 (Android 16, One UI 8). 폰은 통신망, 노트북은 회사 Wi-Fi라 같은 네트워크가 아니다.
- 1차 전송 통로는 USB 케이블 + ADB. 폰에는 앱을 설치하지 않는다.
- 폰 입력은 scrcpy 서버(Apache 2.0)의 제어 프로토콜로 UHID 가상 마우스/키보드를 만들어 넣는다. scrcpy 창·미러링은 쓰지 않는다.
- scrcpy OTG(AOA) 모드는 Windows에서 USB 드라이버 교체가 필요해 제외했다.
- 무선(블루투스 동글)은 나중에 `IPhoneLink`의 다른 구현으로 추가한다. 지금은 만들지 않는다.
- 언어: C# / .NET 8. 앱은 `net8.0-windows`.

## 폴더와 소유권

| 경로 | 소유 | 내용 |
|---|---|---|
| `src/PhoneEdge.Contracts/` | 관리 세션 | 두 모듈 사이의 계약. 다른 세션은 수정 금지 |
| `src/PhoneEdge.Phone/`, `tests/PhoneEdge.Phone.Tests/`, `spike/`, `tools/`, `docs/phase0.md` | 폰 연결 엔진 세션 (Opus) | ADB·scrcpy 서버·UHID, 폰 커서 추적 |
| `src/PhoneEdge.App/`, `tests/PhoneEdge.App.Tests/` | Windows 입력 앱 세션 (Sonnet) | 훅, 경계 감지, HID 변환, 트레이 UI, 업데이트 UI |
| `.github/`, `scripts/`, `Directory.Build.props`, `CHANGELOG.md`, `README.md`, `THIRD-PARTY-NOTICES.md`, `tools/*.ps1`, `tools/versions.json` | Windows 입력 앱 세션 (Sonnet) | 설치 패키지, 릴리스 자동화, 문서 |

## 세션 공통 규칙

- 자기 소유 경로의 파일만 만들고 고친다. 다른 경로는 읽기만 한다.
- 계약(`IPhoneLink`)을 바꿔야 하면 직접 고치지 말고, 필요한 변경과 이유를 최종 보고에 적는다.
- `.sln` 파일을 만들지 않는다. 각자 자기 `.csproj`만 빌드·테스트한다.
- 저장소: https://github.com/ssicci/phone-edge (공개). 커밋·push·태그·릴리스는 관리 세션만 한다. 다른 세션은 파일만 고치고 git 명령으로 상태를 바꾸지 않는다 (`git status`, `git diff` 같은 조회는 괜찮다).
- 공개 저장소이므로 폰 시리얼번호, 사용자 경로, 회사 정보를 코드·테스트·문서에 넣지 않는다.
- 이 폴더(`phone-edge/`) 밖의 파일은 건드리지 않는다.
- 사용자에게 보이는 문구와 보고는 한국어로 쓴다.
- 토큰 절약: 필요한 파일만 읽고, 계획·분석 문서를 따로 만들지 않으며, 최종 보고는 15줄 이내로 한다 (된 것 / 안 된 것 / 검증 방법 / 관리 세션에 요청할 것).
