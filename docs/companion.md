# Twinput Companion (phone app)

Twinput Companion is a small, optional Android app. It turns **USB debugging on when a USB data connection to a computer appears and off when it goes away**, so banking and brokerage apps, which refuse to run while USB debugging is on, work as soon as the cable is unplugged. Twinput on the PC works without it.

Source: [`android/`](../android). Package name: `io.github.ssicci.phoneedge.companion`.

## How it works

1. A foreground service (`CompanionService`, type `specialUse`) runs all the time and shows one persistent notification with the current state ("USB data connection · USB debugging on").
   It starts when the app is opened, after boot (`BOOT_COMPLETED`) and after the app is updated (`MY_PACKAGE_REPLACED`).
2. The service registers, at run time, a receiver for `android.hardware.usb.action.USB_STATE` (sticky, not a public API) and for `ACTION_POWER_CONNECTED` / `ACTION_POWER_DISCONNECTED`. These broadcasts cannot be received by a receiver declared in the manifest, which is why a running service is needed.
3. **Data connection** = `USB_STATE` extra `connected=true`. This means a USB host (a computer) is talking to the phone over the data lines. A wall charger or a charge-only cable gives power (`EXTRA_PLUGGED != 0`) but `connected=false`, so it does **not** turn USB debugging on. (Measured; see below.)
4. On a change of the data-connection state the service writes `Settings.Global.ADB_ENABLED` (`1` on connect, `0` on disconnect) and logs it.
   - "Connected" is acted on at once.
   - "Disconnected" is acted on after 1 s if the phone is no longer powered (cable pulled), or after 6 s if it is still powered. Changing `adb_enabled` makes the phone re-enumerate on USB, which reports `connected=false` for a moment while the cable stays in; the delay keeps that from being taken as an unplug.
   - The service acts only on **transitions**. If you turn USB debugging off by hand while the cable is in (Quick Settings tile, Settings, or Twinput's own "turn off USB debugging on exit"), it stays off until the next unplug/plug.
5. The app screen has a switch for this automatic behaviour (default on). Turning it back on applies the current state immediately.
6. Every state change (connection, USB debugging, who changed it, how long the write took) is kept in an in-app log of the last 20 events, shown on the main screen. A change made by something else (Settings, a computer, Auto Blocker) is logged as such.
7. A Quick Settings tile "USB debugging" turns it on or off by hand.

Developer options as a whole (`development_settings_enabled`) are not touched. `SecureToggle` wraps one `Settings.Global` on/off key, so it can be added as a second instance later if banking apps turn out to check it.

## Permissions and why

| Permission | Why |
|---|---|
| `WRITE_SECURE_SETTINGS` | To write `Settings.Global.ADB_ENABLED`. It cannot be requested in the app; it has the `development` protection flag, so it is granted once over adb: `adb shell pm grant io.github.ssicci.phoneedge.companion android.permission.WRITE_SECURE_SETTINGS`. The grant survives app updates and reboots, and is lost on uninstall. |
| `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_SPECIAL_USE` | The always-on service that receives the USB broadcasts. None of the fixed foreground service types fits, so `specialUse` with a written reason (`PROPERTY_SPECIAL_USE_FGS_SUBTYPE`) is used. |
| `RECEIVE_BOOT_COMPLETED` | Start the service after a reboot. Android 15+ still allows a `specialUse` service to start from `BOOT_COMPLETED`. |
| `POST_NOTIFICATIONS` | Show the status notification (asked on first launch; the service runs without it). |
| `REQUEST_IGNORE_BATTERY_OPTIMIZATIONS` | The "Open battery setting" button opens the system's one-tap "Unrestricted" dialog. |

The app has no internet permission and no dependencies beyond the Android framework and the Kotlin standard library.

## Security model

- **Other computers are stopped by adb's own authorization.** Plugging the phone into an unknown computer turns USB debugging on, but that computer only sees `unauthorized` until someone taps "Allow" on the phone's RSA key prompt. Only computers already marked "Always allow from this computer" get adb access. Revoking USB debugging authorizations in Developer options removes them all.
- **Public USB charging ports.** A port that is only a charger (no data lines, or a wall adapter) gives `connected=false`, so USB debugging stays off. A port with a computer behind it (for example a hostile kiosk) does turn USB debugging on, and it is then protected only by the authorization prompt above: do not tap "Allow" on a prompt you do not expect. If you do not want even that, turn the automatic switch off, or use a charge-only cable.
- `adb_enabled=1` can be refused by a device policy that forbids debugging; the app logs the failure and does nothing else.
- The app never turns USB debugging on while unplugged, so banking apps see it off whenever the phone is not on a data cable.

## Samsung settings

- **Battery: Unrestricted.** Settings > Apps > Twinput Companion > Battery > Unrestricted (the app shows the state and a button). Unrestricted apps are also kept out of "Sleeping apps"; make sure the app is not in "Sleeping apps" / "Deep sleeping apps" (Settings > Battery > Background usage limits).
  Over adb the same allow list can be set with `adb shell cmd deviceidle whitelist +io.github.ssicci.phoneedge.companion`.
- **Auto Blocker** (Settings > Security and privacy > Auto Blocker) can turn USB debugging off by itself (it keeps `rampart_snapshot_adb_enabled`). Keep it off. Its switch is stored in `Settings.Secure` `rampart_main_switch_enabled`, but an app targeting API 31+ gets nothing back for that non-public key (measured on the reference phone), so the app cannot detect it and shows a reminder instead.
- **Notification**: the status notification can be collapsed or its channel silenced; do not turn notifications for the app off entirely if you want to see the state.

## Build

Requirements (versions used):

| Tool | Version | Where |
|---|---|---|
| JDK | Temurin / OpenJDK 21.0.7 (17+ works) | `JAVA_HOME` |
| Android SDK Platform | 37.0 (API 37, Android 17) | `%LOCALAPPDATA%\Android\Sdk\platforms\android-37.0` |
| Android SDK Build-Tools | 37.0.0 | `%LOCALAPPDATA%\Android\Sdk\build-tools\37.0.0` |
| Android SDK Command-line Tools | 23.0 | `%LOCALAPPDATA%\Android\Sdk\cmdline-tools\latest*` |
| Gradle (wrapper, SHA-256 pinned) | 9.9.2 | downloaded by `gradlew` |
| Android Gradle Plugin | 9.5.2 (built-in Kotlin, no separate Kotlin plugin) | Google Maven |

SDK packages are installed with `sdkmanager "platforms;android-37.0" "build-tools;37.0.0"` from the official command-line tools (<https://developer.android.com/studio#command-line-tools-only>).

```powershell
cd android
# local.properties (not committed): sdk.dir=C:/Users/<you>/AppData/Local/Android/Sdk  (or set ANDROID_HOME)
.\gradlew.bat assembleDebug
# -> android\app\build\outputs\apk\debug\app-debug.apk
```

- `minSdk 31` (Android 12): the app relies on behaviour that is uniform from Android 12 on (foreground-service start rules and the boot exemption, mandatory `PendingIntent` mutability flags, `registerReceiver` flags), which keeps the code free of version branches. Phones older than Android 12 no longer get Samsung security updates and are not a target.
- `targetSdk 37` / `compileSdk 37`: the current stable API level (the reference phone reports API 37).
- `gradle.properties` sets `android.overridePathCheck=true` because AGP refuses to build under a path with non-ASCII characters on Windows by default; the build works there.

### Version

The app version always equals Twinput's: Gradle reads `<Version>X.Y.Z</Version>` from the repository's `Directory.Build.props`.

- `versionName` = `X.Y.Z`
- `versionCode` = `X*10000 + Y*100 + Z` (0.4.0 → 400, 1.2.3 → 10203). Minor and patch must stay below 100; the build fails otherwise.

Bump the version only in `Directory.Build.props`.

### Release signing

Android installs an update over an existing app only when both are signed with the same key. Release builds therefore use one long-lived key:

| | |
|---|---|
| Keystore | PKCS12, alias `phoneedge-companion`, kept **outside the repository on the maintainer's PC** (store and key password are the same) |
| Certificate | `CN=Twinput Companion` |
| SHA-256 fingerprint | `42:69:FA:0B:05:97:14:76:65:DA:03:9F:9A:00:15:FA:8A:51:4D:1B:A2:94:C7:37:4A:03:8C:9F:BB:30:FC:09` |
| GitHub Actions secrets | `COMPANION_KEYSTORE_BASE64`, `COMPANION_KEYSTORE_PASSWORD`, `COMPANION_KEY_ALIAS`, `COMPANION_KEY_PASSWORD` |

`app/build.gradle.kts` reads the signing values **only from environment variables**:

| Variable | Value |
|---|---|
| `COMPANION_KEYSTORE_FILE` | Path to the keystore file (in CI: the base64 secret decoded to a temporary file) |
| `COMPANION_KEYSTORE_PASSWORD` | Keystore password |
| `COMPANION_KEY_ALIAS` | `phoneedge-companion` |
| `COMPANION_KEY_PASSWORD` | Key password (same as the keystore password) |

If any is missing, or the file does not exist, `assembleRelease` stops at once with an error naming them. `assembleDebug` never needs them.

```powershell
cd android
.\gradlew.bat assembleRelease
# -> android\app\build\outputs\apk\release\app-release.apk (minified, about 60 KB)
```

Check a release APK before shipping or installing it:

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\build-tools\37.0.0\apksigner.bat" verify --print-certs android\app\build\outputs\apk\release\app-release.apk
```

The `certificate SHA-256 digest` must equal the fingerprint above.

**If the key is lost or replaced**, no new APK can update the installed app (Android reports a signature mismatch). Each phone must then:

1. Uninstall the old app (`adb uninstall io.github.ssicci.phoneedge.companion`). The in-app log and settings are lost.
2. Install the APK signed with the new key.
3. Grant `WRITE_SECURE_SETTINGS` again (`adb shell pm grant ...`), and set battery to Unrestricted again; both are removed with the uninstall.

Then publish the new fingerprint here and in the release notes, and replace the four GitHub secrets. Keep an offline backup of the keystore and its password to avoid this.

Debug builds are signed with each PC's own debug key, so a debug APK cannot be replaced by a release APK (or one from another PC) without the same uninstall → install → grant steps.

## Measured results

2026-11-25, Galaxy Z Fold7 (SM-F966N, device reports Android 17 / API 37), debug build, battery set to Unrestricted, screen off with the app in the background, PC already "always allowed". Phone times are from the app's logcat, PC times from polling `adb devices` every 200 ms; the two clocks agreed within about 0.1 s.

| Test | Result |
|---|---|
| `pm grant ... WRITE_SECURE_SETTINGS` | App shows "granted"; writes work |
| Writing `adb_enabled` | 2–6 ms per write (6 writes) |
| Unplug from PC (×3) | `ACTION_POWER_DISCONNECTED` first, `USB_STATE connected=false` 2.8 s later, USB debugging off 1.0 s after that: **3.8 s** from unplug to off each time |
| Plug into PC (×3) | `USB_STATE connected=true` arrives first (before power), USB debugging on within 2–5 ms; the PC lists the phone as `device` (never `unauthorized`) **1.8 / 2.7 / 3.2 s** after the plug |
| Re-enumeration after turning adb on | `configured` goes false then true; `connected` stays true, so the 6 s "still powered" delay was never needed |
| Wall charger | `plugged=ac`, `connected=false`: USB debugging stayed off |
| Charge-only cable | Not tested (none at hand); expected to behave like the wall charger since no data lines means no `connected=true` |
| Auto Blocker switch | `rampart_main_switch_enabled` exists in `Settings.Secure` (0 on this phone) but the app reads nothing, so the app shows the reminder |

Most of the 3.8 s on unplug is Samsung's 2.8 s delay before `USB_STATE` reports the disconnect. If faster is needed, `ACTION_POWER_DISCONNECTED` (which arrives at once) could be used as the unplug signal; not done because the current behaviour is the one measured.
