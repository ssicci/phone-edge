# PhoneEdge

A Windows app that lets you use the laptop's mouse and keyboard on an Android phone placed next to it, by moving the cursor across the screen edge. Korean terms in parentheses are the words used in the app UI and the code.

## Language

**Laptop mode** (노트북 모드):
The state where mouse and keyboard input goes to Windows as usual.

**Phone mode** (폰 모드):
The state where mouse and keyboard input is not passed to Windows and is sent to the phone instead.
_Avoid_: remote mode, control mode

**Edge crossing** (경계 전환):
Switching between laptop mode and phone mode because the cursor crossed a screen edge.
_Avoid_: switching, handoff

**Entry edge** (진입 경계):
The edge of the phone screen where the phone cursor appears when phone mode starts. With the phone on the right of the laptop, it is the left edge of the phone screen.

**Emergency return** (비상 복귀):
Returning to laptop mode immediately, regardless of where the phone cursor is, because of the hotkey or a lost connection.

**Link** (전송 통로):
The connection that carries input to the phone. Today it is USB/ADB only, and it corresponds to exactly one implementation of `IPhoneLink`.
_Avoid_: connection, channel

**Virtual input device** (가상 입력 장치):
A mouse or keyboard created inside the phone with UHID, which the phone treats as real hardware.

## Relationships

- An **edge crossing** into **phone mode** makes the phone cursor appear at the **entry edge**
- When the phone cursor crosses the **entry edge** again, input returns to **laptop mode**
- If the **link** drops, an **emergency return** always happens
- One **link** creates the **virtual input devices** (a mouse and a keyboard)
