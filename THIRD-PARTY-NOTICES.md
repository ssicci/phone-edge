# Third-party notices

Twinput bundles or uses the software below. The full license texts are in the `licenses\` folder of the installation.

## scrcpy server (Apache License 2.0)

Twinput uses the `scrcpy-server` that creates the input devices on the phone, unmodified (v5.0.1).

- Copyright: Copyright (C) 2018 Genymobile, Copyright (C) 2018-2024 Romain Vimont
- Source: https://github.com/Genymobile/scrcpy
- License: Apache License 2.0 (`licenses\scrcpy-LICENSE.txt`)

## Android SDK Platform-Tools (adb)

To talk to the phone over USB, Twinput includes the `adb.exe`, `AdbWinApi.dll` and `AdbWinUsbApi.dll` distributed by Google, unmodified (r37.0.3).

- Source: https://developer.android.com/tools/releases/platform-tools
- License: the Android SDK license and the licenses of the included components (`licenses\platform-tools-NOTICE.txt`)

## Velopack (MIT License)

Used for installation and automatic updates.

- Source: https://github.com/velopack/velopack
- License: MIT

## .NET runtime (MIT License)

The .NET 8 runtime is included (self-contained), so .NET does not have to be installed on the PC.

- Source: https://github.com/dotnet/runtime
- License: MIT
