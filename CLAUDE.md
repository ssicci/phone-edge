# PhoneEdge (working title)

A Windows tray app that sends the mouse and keyboard to a USB-connected Android phone when the laptop cursor crosses the right edge of the screen.
Terms follow `CONTEXT.md`. A `CONTEXT.md` in a parent folder, if any, does not apply to this project.

## Decisions

- Reference device: Samsung Galaxy Z Fold7 (Android 16, One UI 8). The phone is on the mobile network and the laptop is on the office Wi-Fi, so they are not on the same network.
- The first link is a USB cable + ADB. Nothing is installed on the phone.
- Phone input goes through the scrcpy server's (Apache 2.0) control protocol, which creates a UHID virtual mouse and keyboard. scrcpy's window and mirroring are not used.
- scrcpy's OTG (AOA) mode is excluded because it needs a USB driver swap on Windows.
- Wireless (a Bluetooth dongle) will be added later as another implementation of `IPhoneLink`. It is not built now.
- Language: C# / .NET 8. The app targets `net8.0-windows10.0.19041.0`.

## Folders and ownership

| Path | Owner | Contents |
|---|---|---|
| `src/PhoneEdge.Contracts/` | Management session | The contract between the two modules. Other sessions must not edit it |
| `src/PhoneEdge.Phone/`, `tests/PhoneEdge.Phone.Tests/`, `spike/`, `tools/` (except the items below), `docs/phase0.md` | Phone engine session (Opus) | ADB, scrcpy server, UHID, phone cursor tracking |
| `src/PhoneEdge.App/`, `tests/PhoneEdge.App.Tests/` | Windows input app session (Sonnet) | Hooks, edge detection, HID conversion, tray UI, update UI |
| `.github/`, `scripts/`, `Directory.Build.props`, `CHANGELOG.md`, `README.md`, `THIRD-PARTY-NOTICES.md`, `docs/images/`, `tools/*.ps1`, `tools/versions.json` | Windows input app session (Sonnet) | Installer, release automation, documentation |

## Rules for every session

- Create and edit files only in your own paths. Read other paths only.
- If the contract (`IPhoneLink`) must change, do not edit it. Describe the needed change and the reason in your final report.
- Do not create a `.sln` file. Each session builds and tests only its own `.csproj`.
- Repository: https://github.com/ssicci/phone-edge (public). Only the management session commits, pushes, tags and releases. Other sessions only edit files and do not change repository state with git commands (read-only commands such as `git status` and `git diff` are fine).
- The repository is public: do not put phone serial numbers, user paths or company information in code, tests or documents.
- Do not touch files outside this folder (`phone-edge/`).
- **Language**: all `.md` documents are written in English. `CHANGELOG.md` is English followed by Korean for each version, because its text is shown unchanged in the app's update window and the app UI is Korean. App UI text and session reports to the user are in Korean.
- Documentation images live in `docs/images/`. Regenerate the PNGs with `pwsh scripts/make-docs-images.ps1` after changing the app UI. The SVG diagrams are hand-written.
- Token economy: read only the files you need, do not write separate planning or analysis documents, and keep the final report within 15 lines (done / not done / how it was verified / requests to the management session).
