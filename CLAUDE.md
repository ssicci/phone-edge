# Twinput

A Windows tray app that sends the mouse and keyboard to a USB-connected Android phone when the laptop cursor crosses the right edge of the screen.
Terms follow `CONTEXT.md`. A `CONTEXT.md` in a parent folder, if any, does not apply to this project.

## Decisions

- Reference device: Samsung Galaxy Z Fold7 (Android 16, One UI 8). The phone is on the mobile network and the laptop is on the office Wi-Fi, so they are not on the same network.
- The first link is a USB cable + ADB. Mouse and keyboard input needs nothing installed on the phone.
- Optional phone companion app (decided 2026-11-25): banking and brokerage apps refuse to run while USB debugging is on, so a small Android app turns USB debugging on when a USB data connection appears and off when it goes away. Twinput grants it `WRITE_SECURE_SETTINGS` once over adb. adb's own "always allow this computer" authorization is what keeps other PCs out. The app needs a foreground service (persistent notification) and Samsung's "unrestricted" battery setting. Twinput must keep working without it.
- Phone input goes through the scrcpy server's (Apache 2.0) control protocol, which creates a UHID virtual mouse and keyboard. scrcpy's window and mirroring are not used.
- scrcpy's OTG (AOA) mode is excluded because it needs a USB driver swap on Windows.
- Wireless link (decided 2026-11-27): a **Waveshare ESP32-S3-Zero** dongle on the PC's USB port acts as a BLE keyboard + mouse (HID over GATT) to the phone, as another implementation of `IPhoneLink`. The laptop's built-in Bluetooth cannot do this: spike 3 (`spike/Twinput.BtHidSpike3`) showed Windows delivers read/subscribe requests for an app's custom GATT service but never for an app-published HID service (0x1812).
  - The phone bonds with the dongle, not with the PC, so the dongle can move between PCs without re-pairing. The dongle advertises as "Twinput" with a keyboard/mouse appearance; Twinput flashes and updates its firmware automatically.
  - Default experience needs no phone app: the phone screen size comes from a default or a model choice, and a hotkey returns to the laptop as a fallback. The phone companion app is optional; when installed it reports screen size and rotation to the dongle over BLE for exact edge crossing.
  - The USB/adb link stays and is used when no dongle is present.
- Product name (decided 2026-11-27): **Twinput** (twin + input; Korean "트윈풋"), formerly PhoneEdge. The Velopack package id is `Twinput`; Twinput imports an old PhoneEdge install's settings on first run. The companion app keeps its Android application id `io.github.ssicci.phoneedge.companion` so installed copies keep updating and keep their permission.
- Language: C# / .NET 8. The app targets `net8.0-windows10.0.19041.0`.

## Folders and ownership

| Path | Owner | Contents |
|---|---|---|
| `src/Twinput.Contracts/` | Management session | The contract between the two modules. Other sessions must not edit it |
| `src/Twinput.Phone/`, `tests/Twinput.Phone.Tests/`, `spike/`, `tools/` (except the items below), `docs/phase0.md` | Phone engine session (Opus) | ADB, scrcpy server, UHID, phone cursor tracking |
| `src/Twinput.App/`, `tests/Twinput.App.Tests/` | Windows input app session (Sonnet) | Hooks, edge detection, HID conversion, tray UI, update UI |
| `android/`, `docs/companion.md` | Phone companion app session (Opus) | Android app (Kotlin), its build and signing setup |
| `.github/`, `scripts/`, `Directory.Build.props`, `CHANGELOG.md`, `README.md`, `README.ko.md`, `THIRD-PARTY-NOTICES.md`, `docs/images/`, `tools/*.ps1`, `tools/versions.json` | Windows input app session (Sonnet) | Installer, release automation, documentation |

## Rules for every session

- Create and edit files only in your own paths. Read other paths only.
- If the contract (`IPhoneLink`) must change, do not edit it. Describe the needed change and the reason in your final report.
- Do not create a `.sln` file. Each session builds and tests only its own `.csproj`.
- Repository: https://github.com/ssicci/twinput (public; renamed from `phone-edge`, the local folder keeps its old name). Only the management session commits, pushes, tags and releases. Other sessions only edit files and do not change repository state with git commands (read-only commands such as `git status` and `git diff` are fine).
- The repository is public: do not put phone serial numbers, user paths or company information in code, tests or documents.
- Do not touch files outside this folder (`phone-edge/`).
- **Language**:
  - Documents (`.md`) are written in English. The exceptions are `README.ko.md` (the Korean README, which must keep the same structure as `README.md`) and `CHANGELOG.md`, which is English followed by Korean for each version (a `---` line separates them) because the app's update window shows the part in the current language.
  - The app UI is shown in Korean or English (Windows display language by default, selectable from the tray menu). Every user-visible string lives in `src/Twinput.App/Resources/Strings.resx` (English, the default) and `Strings.ko.resx` (Korean). Add every new string to **both** files and reach it through `L.T("Key")`; never hard-code UI text. Logs and the phone engine's `Detail` text stay untranslated (developer-facing).
  - Session reports to the user are in Korean.
- Documentation images live in `docs/images/en/` and `docs/images/ko/` (one set per language). Regenerate the PNGs with `pwsh scripts/make-docs-images.ps1` after changing the app UI or any text. The SVG diagrams are hand-written, one copy per language.
- WinForms windows in this tray app are opened with `Show()` (modeless). A button's `DialogResult` does not close a modeless window, so every closing button must set `DialogResult` and call `Close()` in its `Click` handler, and its test must assert that the window actually closed (this bug shipped twice: the help window in 0.3 and the phone position window in 0.7.0).
- Token economy: read only the files you need, do not write separate planning or analysis documents, and keep the final report within 15 lines (done / not done / how it was verified / requests to the management session).
