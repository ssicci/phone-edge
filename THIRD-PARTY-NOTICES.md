# 제3자 소프트웨어 고지

PhoneEdge는 아래 소프트웨어를 함께 배포하거나 사용합니다. 전체 라이선스 문구는 설치 폴더의 `licenses\` 폴더에 있습니다.

## scrcpy 서버 (Apache License 2.0)

폰에 입력 장치를 만드는 `scrcpy-server`를 그대로 사용합니다 (v5.0.1, 수정 없음).

- 저작권: Copyright (C) 2018 Genymobile, Copyright (C) 2018-2024 Romain Vimont
- 출처: https://github.com/Genymobile/scrcpy
- 라이선스: Apache License 2.0 (`licenses\scrcpy-LICENSE.txt`)

## Android SDK Platform-Tools (adb)

USB로 폰과 통신하기 위해 Google이 배포하는 `adb.exe`, `AdbWinApi.dll`, `AdbWinUsbApi.dll`을 그대로 포함합니다 (r37.0.3, 수정 없음).

- 출처: https://developer.android.com/tools/releases/platform-tools
- 라이선스: Android SDK 라이선스와 포함된 구성요소의 라이선스 (`licenses\platform-tools-NOTICE.txt`)

## Velopack (MIT License)

설치와 자동 업데이트에 사용합니다.

- 출처: https://github.com/velopack/velopack
- 라이선스: MIT

## .NET 런타임 (MIT License)

사용자 PC에 .NET이 없어도 실행되도록 .NET 8 런타임을 함께 포함합니다 (self-contained).

- 출처: https://github.com/dotnet/runtime
- 라이선스: MIT
