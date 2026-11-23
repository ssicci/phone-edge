# Phase 0 verification results (2026-11-16, SM-F966N, unfolded 1968×2184, device reports Android 17)

Tools: platform-tools r37.0.3 (SHA-1 matched), scrcpy-server v5.0.1 (SHA-256 matched) — see `tools/versions.json`
Reproduce: `dotnet run --project spike/PhoneEdge.Spike -- demo|exit|enter|curve|accel`

| Item | Result | Evidence |
|---|---|---|
| (a) adb recognizes the phone | Pass | `adb devices` → `<serial> device` (laptop) |
| (b) Server + UHID | Pass | Ran the server with `video=false audio=false control=true`; "PhoneEdge Mouse/Keyboard" were created (Classes: CURSOR). `getevent` showed REL_X/Y, BTN_MOUSE down/up, and KEY_H·E·L·L·O down/up. `dumpsys` showed the pointer coordinates moving |
| (c) Cursor tracking | Pass (conditional) | See below |
| (d) Screen size detection | Implemented, folding not measured | Reads the MouseCursorController viewport (displayId, logicalFrame) from `dumpsys input` every second. On the Fold7 the cover display (display 1, 1080×2520) and the main display (display 0) are separate logical displays, so the first `cur=` in `dumpsys window displays` gives the wrong screen |

## (c) Chosen model: corner pin + clamp + exit threshold (relative-motion UHID mouse)

- On entry, a large negative move pins the cursor to the top-left corner (0,0), then it moves down by y. After that x and y are accumulated as gain × input and clamped at the screen edges like a real phone. Pushing more than 24 px further left at x=0 triggers the exit (no exit while a button is held).
- Gain: measured automatically on connect (top-left → rest 600 ms → one +200-count report → read the real position). Fold7, pointer speed 0: **2.680 px/count**.
- Acceleration measured (pointer acceleration on, the default): up to 625 counts/s 2.680, 1250 → 3.18, 2500 → 4.51, 3750 → 6.49 (the same curve at 2 ms and 8 ms report intervals).
- Acceleration off: **fixed 2.680** over the whole 1000–37500 counts/s range → the linear model is accurate.
- Exit measured: ExitRequested fired at 410 counts left after 400 counts right (theory: 409).
- Entry y: with acceleration on, the vertical move right after the corner move jumped (0.2 → 1957 px). With a 60 ms rest between the two moves the error is ≤1 px (437/1091/1747 expected 437/1092/1746).
- Rejected: absolute-coordinate HID (a digitizer means a stylus, so wheel and mouse behaviour differ), and reading the pointer position continuously (one `dumpsys` takes 0.3 s, too slow for real-time decisions; used only for calibration).

## Phone settings needed

- Developer options → turn on USB debugging, and allow this PC when asked.
- **Turn off mouse pointer acceleration** (in the phone's mouse settings; turning it off through the adb key `system/mouse_pointer_acceleration_enabled=0` was verified and then restored). With it on, the model falls behind by up to 2.4× during fast moves and the exit is late.
