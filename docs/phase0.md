# 0단계 검증 결과 (2026-11-16, SM-F966N, 펼친 상태 1968×2184, 기기 보고 Android 17)

도구: platform-tools r37.0.3 (SHA-1 일치), scrcpy-server v5.0.1 (SHA-256 일치) — `tools/VERSIONS.txt`
재현: `dotnet run --project spike/PhoneEdge.Spike -- demo|exit|enter|curve|accel`

| 항목 | 결과 | 근거 |
|---|---|---|
| (a) adb 인식 | 통과 | `adb devices` → `<시리얼> device` (회사 노트북) |
| (b) 서버 + UHID | 통과 | `video=false audio=false control=true`로 서버 실행, "PhoneEdge Mouse/Keyboard" 생성(Classes: CURSOR). getevent에서 REL_X/Y, BTN_MOUSE down/up, KEY_H·E·L·L·O down/up 확인. dumpsys에서 포인터 좌표 이동 확인 |
| (c) 커서 추적 | 통과 (조건부) | 아래 |
| (d) 화면 크기 감지 | 구현, 접기 실측 안 함 | `dumpsys input`의 MouseCursorController viewport(displayId, logicalFrame)를 1초마다 읽음. 폴드7은 커버(display 1, 1080×2520)와 메인(display 0)이 별도 논리 디스플레이라 `dumpsys window displays`의 첫 `cur=`은 틀린 화면을 줌 |

## (c) 선택: 구석 고정 + 클램프 모델 + 이탈 문턱 (상대 이동 UHID 마우스)
- 진입 때 큰 음수 이동으로 좌상단 (0,0)에 붙인 뒤 y만큼 내린다. 이후 x·y를 이득×입력으로 누적하고 폰처럼 화면 끝에서 클램프한다. x=0에서 왼쪽으로 24 px 넘게 더 밀면 이탈(버튼 누른 채로는 이탈 안 함).
- 이득: 연결 때 자동 측정(좌상단 → 600 ms 쉼 → +200 카운트 단일 리포트 → 실제 위치 읽기). 폴드7, 포인터 속도 0: **2.680 px/카운트**.
- 가속 실측 (포인터 가속 켜짐, 기본값): 625 cnt/s 이하 2.680, 1250 → 3.18, 2500 → 4.51, 3750 → 6.49 (2 ms·8 ms 주기 모두 같은 곡선).
- 가속 끔: 1000~37500 cnt/s 전 구간 **2.680 고정** → 선형 모델이 정확함.
- 이탈 실측: 오른쪽 400 카운트 → 왼쪽 410 카운트에서 ExitRequested (이론 409).
- 진입 y: 가속 켜짐에서 구석 이동 직후 세로 이동이 튐(0.2 → 1957 px). 두 이동 사이 60 ms 쉬면 오차 ≤1 px (437/1091/1747 기대 437/1092/1746).
- 탈락: 절대좌표 HID(디지타이저는 스타일러스 의미라 휠·마우스 동작이 다름), 포인터 위치 상시 읽기(dumpsys 1회 0.3 s라 실시간 판정에 못 씀, 보정용으로만 사용).

## 폰에서 필요한 설정
- 개발자 옵션 → USB 디버깅 켜기, 연결 시 이 PC 허용.
- **마우스 포인터 가속 끄기** (설정의 마우스 항목. adb 키 `system/mouse_pointer_acceleration_enabled=0`으로 끔 확인 후 원래대로 복원함). 켜 두면 빠른 이동에서 모델이 최대 2.4배 뒤처져 이탈이 늦어짐.
