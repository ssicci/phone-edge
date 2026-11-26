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
The edge of the phone screen where the phone cursor appears when phone mode starts; it is the phone edge that faces the laptop. With the phone on the right of the laptop it is the left edge of the phone screen, with the phone above the laptop it is the bottom edge.

**Phone slot** (폰 자리):
Where the phone sits next to the laptop's screens: one monitor, one of its four sides, and the span along that side where crossing leads to the phone. Only a span that no other monitor touches can be a phone slot.
_Avoid_: phone side, phone position (these meant only left/right before 0.7)

**Monitor set** (모니터 구성):
The monitors connected at one time, identified by each monitor's stable ID and their arrangement in Windows. Plugging the laptop into a different desk gives a different monitor set.

**Layout profile** (배치 프로필):
The phone slot remembered for one monitor set. PhoneEdge picks the profile that matches the current monitor set.
_Avoid_: preset, location

**Accidental entry guard** (오진입 방지):
The rules that keep ordinary mouse use near a screen edge from entering phone mode: the ends of the phone slot are excluded, and the cursor must be pushed a short distance past the edge.

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
- A **monitor set** has at most one **layout profile**, and a **layout profile** has exactly one **phone slot**
- An **edge crossing** into **phone mode** happens only inside the **phone slot**, and only after the **accidental entry guard** lets it through
- The **phone slot**'s side decides the **entry edge**: the phone edge facing that side (right side → left entry edge, top side → bottom entry edge)
- A position along the **phone slot** maps to the same relative position along the **entry edge**, and back

## Flagged ambiguities

- "Phone position" (폰 위치) meant only right/left of the laptop in 0.6 — resolved: from 0.7 it is a **phone slot** stored per **layout profile**.
